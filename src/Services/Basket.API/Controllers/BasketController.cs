using Basket.API.Entities;
using Basket.API.Repositories;
using Basket.API.Services;
using EventBus.Messages.Events;
using MassTransit; // <--- Importante
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using System.Net;

namespace Basket.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BasketController : ControllerBase
    {
        private readonly IBasketRepository _repository;
        private readonly IPublishEndpoint _publishEndpoint; // <--- Publicador de eventos
        private readonly ICatalogService _catalogService;
        private readonly ILogger<BasketController> _logger;

        public BasketController(
            IBasketRepository repository,
            IPublishEndpoint publishEndpoint,
            ICatalogService catalogService,
            ILogger<BasketController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{userName}", Name = "GetBasket")]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> GetBasket(string userName)
        {
            var basket = await _repository.GetBasket(userName);
            return Ok(basket ?? new ShoppingCart(userName));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
        public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
        {
            // Valida cada item contra o Catalog.API antes de gravar o carrinho.
            // Importante: o preço final vem do catálogo, não do que o cliente
            // enviou no corpo da requisição — sem isso, qualquer um poderia
            // adicionar um item ao carrinho com o preço que quisesse.
            foreach (var item in basket.Items)
            {
                CatalogProductDto? product;

                try
                {
                    product = await _catalogService.GetProductAsync(item.ProductId);
                }
                catch (BrokenCircuitException)
                {
                    // Circuit breaker aberto: o Catalog.API já falhou demais
                    // recentemente, então nem tentamos a chamada. Falha explícita
                    // em vez de aceitar um preço não verificado.
                    _logger.LogWarning("Circuito do Catalog.API aberto. Checkout/atualização de carrinho recusada para {UserName}.", basket.UserName);
                    return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                        "Catálogo temporariamente indisponível. Tente novamente em instantes.");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Falha ao consultar o Catalog.API para o produto {ProductId}.", item.ProductId);
                    return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                        "Não foi possível validar os itens do carrinho no momento. Tente novamente em instantes.");
                }

                if (product is null)
                {
                    return BadRequest($"Produto '{item.ProductId}' não encontrado no catálogo.");
                }

                item.ProductName = product.Name;
                item.Price = product.Price;
            }

            return Ok(await _repository.UpdateBasket(basket));
        }

        [HttpDelete("{userName}", Name = "DeleteBasket")]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteBasket(string userName)
        {
            await _repository.DeleteBasket(userName);
            return Ok();
        }

        // --- NOVO ENDPOINT DE CHECKOUT VIA RABBITMQ ---
        [HttpPost]
        [Route("[action]")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Checkout([FromBody] BasketCheckout basketCheckout)
        {
            // 1. Busca o carrinho existente do usuário
            var basket = await _repository.GetBasket(basketCheckout.UserName);
            if (basket == null)
            {
                return NotFound();
            }

            // 2. Cria o evento e mapeia os dados do checkout com o total do carrinho
            var eventMessage = new BasketCheckoutEvent
            {
                CheckoutId = Guid.NewGuid(),
                UserName = basketCheckout.UserName,
                TotalPrice = basket.TotalPrice,
                FirstName = basketCheckout.FirstName,
                LastName = basketCheckout.LastName,
                EmailAddress = basketCheckout.EmailAddress,
                AddressLine = basketCheckout.AddressLine,
                Country = basketCheckout.Country,
                State = basketCheckout.State,
                ZipCode = basketCheckout.ZipCode
            };

            // 3. Publica o evento no RabbitMQ
            await _publishEndpoint.Publish<BasketCheckoutEvent>(eventMessage);

            // 4. Remove o carrinho após o checkout bem-sucedido
            await _repository.DeleteBasket(basketCheckout.UserName);

            return Accepted(eventMessage);
        }
    }
}