using Basket.API.Entities;
using Basket.API.Repositories;
using EventBus.Messages.Events;
using MassTransit; // <--- Importante
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Basket.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BasketController : ControllerBase
    {
        private readonly IBasketRepository _repository;
        private readonly IPublishEndpoint _publishEndpoint; // <--- Publicador de eventos

        public BasketController(IBasketRepository repository, IPublishEndpoint publishEndpoint)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
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
        public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
        {
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