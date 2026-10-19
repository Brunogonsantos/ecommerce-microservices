using Basket.API.Controllers;
using Basket.API.Entities;
using Basket.API.Repositories;
using Basket.API.Services;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker;
using Xunit;

namespace Basket.UnitTests
{
    public class BasketControllerTests
    {
        private readonly Mock<IBasketRepository> _repositoryMock = new();
        private readonly Mock<IPublishEndpoint> _publishEndpointMock = new();
        private readonly Mock<ICatalogService> _catalogServiceMock = new();
        private readonly BasketController _controller;

        public BasketControllerTests()
        {
            _controller = new BasketController(
                _repositoryMock.Object,
                _publishEndpointMock.Object,
                _catalogServiceMock.Object,
                Mock.Of<ILogger<BasketController>>());
        }

        private static ShoppingCart BuildCarrinhoComUmItem(string productId, decimal precoEnviadoPeloCliente)
        {
            var carrinho = new ShoppingCart("usuario-teste");
            carrinho.Items.Add(new ShoppingCartItem
            {
                ProductId = productId,
                ProductName = "nome enviado pelo cliente (deve ser ignorado)",
                Price = precoEnviadoPeloCliente,
                Quantity = 1
            });
            return carrinho;
        }

        private static ShoppingCart BuildCarrinhoParaCheckout()
        {
            var carrinho = new ShoppingCart("usuario-teste");
            carrinho.Items.Add(new ShoppingCartItem { ProductId = "p1", ProductName = "Produto", Price = 50m, Quantity = 2 });
            return carrinho;
        }

        // ---------- UpdateBasket ----------

        [Fact]
        public async Task UpdateBasket_QuandoProdutoExiste_UsaPrecoDoCatalogoNaoODoCliente()
        {
            var productId = Guid.NewGuid().ToString();
            var carrinho = BuildCarrinhoComUmItem(productId, precoEnviadoPeloCliente: 0.99m);

            _catalogServiceMock
                .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogProductDto { Id = productId, Name = "Produto Real", Price = 2500m });

            _repositoryMock
                .Setup(r => r.UpdateBasket(It.IsAny<ShoppingCart>()))
                .ReturnsAsync((ShoppingCart c) => c);

            var resultado = await _controller.UpdateBasket(carrinho);

            var okResult = Assert.IsType<OkObjectResult>(resultado.Result);
            var carrinhoSalvo = Assert.IsType<ShoppingCart>(okResult.Value);
            Assert.Equal(2500m, carrinhoSalvo.Items[0].Price);
            Assert.Equal("Produto Real", carrinhoSalvo.Items[0].ProductName);
        }

        [Fact]
        public async Task UpdateBasket_DescartaPendingCheckoutIdEnviadoPeloCliente()
        {
            var productId = Guid.NewGuid().ToString();
            var carrinho = BuildCarrinhoComUmItem(productId, precoEnviadoPeloCliente: 10m);
            carrinho.PendingCheckoutId = Guid.NewGuid(); // o cliente tenta impor um id

            _catalogServiceMock
                .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CatalogProductDto { Id = productId, Name = "Produto", Price = 10m });

            _repositoryMock
                .Setup(r => r.UpdateBasket(It.IsAny<ShoppingCart>()))
                .ReturnsAsync((ShoppingCart c) => c);

            var resultado = await _controller.UpdateBasket(carrinho);

            var okResult = Assert.IsType<OkObjectResult>(resultado.Result);
            var carrinhoSalvo = Assert.IsType<ShoppingCart>(okResult.Value);
            Assert.Null(carrinhoSalvo.PendingCheckoutId);
        }

        [Fact]
        public async Task UpdateBasket_QuandoProdutoNaoExisteNoCatalogo_RetornaBadRequest()
        {
            var productId = Guid.NewGuid().ToString();
            var carrinho = BuildCarrinhoComUmItem(productId, precoEnviadoPeloCliente: 10m);

            _catalogServiceMock
                .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CatalogProductDto?)null);

            var resultado = await _controller.UpdateBasket(carrinho);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
            _repositoryMock.Verify(r => r.UpdateBasket(It.IsAny<ShoppingCart>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBasket_QuandoCircuitBreakerAberto_RetornaServiceUnavailable()
        {
            var productId = Guid.NewGuid().ToString();
            var carrinho = BuildCarrinhoComUmItem(productId, precoEnviadoPeloCliente: 10m);

            _catalogServiceMock
                .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BrokenCircuitException("circuito aberto"));

            var resultado = await _controller.UpdateBasket(carrinho);

            var objectResult = Assert.IsType<ObjectResult>(resultado.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
            _repositoryMock.Verify(r => r.UpdateBasket(It.IsAny<ShoppingCart>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBasket_QuandoCatalogoInacessivel_RetornaServiceUnavailable()
        {
            var productId = Guid.NewGuid().ToString();
            var carrinho = BuildCarrinhoComUmItem(productId, precoEnviadoPeloCliente: 10m);

            _catalogServiceMock
                .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("conexão recusada"));

            var resultado = await _controller.UpdateBasket(carrinho);

            var objectResult = Assert.IsType<ObjectResult>(resultado.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        }

        // ---------- Checkout ----------

        [Fact]
        public async Task Checkout_QuandoCarrinhoNaoExiste_RetornaNotFound()
        {
            _repositoryMock.Setup(r => r.GetBasket("inexistente")).ReturnsAsync((ShoppingCart?)null);

            var resultado = await _controller.Checkout(new BasketCheckout { UserName = "inexistente" });

            Assert.IsType<NotFoundResult>(resultado);
            _publishEndpointMock.Verify(
                p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Checkout_PrimeiraTentativa_GeraCheckoutIdNaoVazio_EPublicaOEvento()
        {
            var carrinho = BuildCarrinhoParaCheckout();

            _repositoryMock.Setup(r => r.GetBasket("usuario-teste")).ReturnsAsync(carrinho);
            _repositoryMock.Setup(r => r.UpdateBasket(It.IsAny<ShoppingCart>())).ReturnsAsync((ShoppingCart c) => c);
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste")).Returns(Task.CompletedTask);

            BasketCheckoutEvent? eventoPublicado = null;
            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BasketCheckoutEvent, CancellationToken>((evt, _) => eventoPublicado = evt)
                .Returns(Task.CompletedTask);

            var resultado = await _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" });

            Assert.IsType<AcceptedResult>(resultado);
            Assert.NotNull(eventoPublicado);
            Assert.NotEqual(Guid.Empty, eventoPublicado!.CheckoutId);
            Assert.Equal(carrinho.PendingCheckoutId, eventoPublicado.CheckoutId);
            _repositoryMock.Verify(r => r.DeleteBasket("usuario-teste"), Times.Once);
        }

        [Fact]
        public async Task Checkout_GravaOCheckoutIdNoCarrinhoAntesDePublicar()
        {
            // Invariante central do desenho: o id precisa estar persistido ANTES do
            // Publish. Se a ordem fosse publicar -> gravar, uma queda entre os dois
            // passos deixaria o evento no ar sem id guardado para reaproveitar.
            var carrinho = BuildCarrinhoParaCheckout();
            var chamadas = new List<string>();

            _repositoryMock.Setup(r => r.GetBasket("usuario-teste")).ReturnsAsync(carrinho);
            _repositoryMock.Setup(r => r.UpdateBasket(It.IsAny<ShoppingCart>()))
                .Callback<ShoppingCart>(_ => chamadas.Add("gravar-id"))
                .ReturnsAsync((ShoppingCart c) => c);
            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BasketCheckoutEvent, CancellationToken>((_, _) => chamadas.Add("publicar"))
                .Returns(Task.CompletedTask);
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste"))
                .Callback<string>(_ => chamadas.Add("apagar-carrinho"))
                .Returns(Task.CompletedTask);

            await _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" });

            Assert.Equal(new[] { "gravar-id", "publicar", "apagar-carrinho" }, chamadas);
        }

        [Fact]
        public async Task Checkout_QuandoJaExisteCheckoutIdPendente_ReaproveitaOMesmoIdSemRegravar()
        {
            var idExistente = Guid.NewGuid();
            var carrinho = BuildCarrinhoParaCheckout();
            carrinho.PendingCheckoutId = idExistente;

            _repositoryMock.Setup(r => r.GetBasket("usuario-teste")).ReturnsAsync(carrinho);
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste")).Returns(Task.CompletedTask);

            BasketCheckoutEvent? eventoPublicado = null;
            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BasketCheckoutEvent, CancellationToken>((evt, _) => eventoPublicado = evt)
                .Returns(Task.CompletedTask);

            await _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" });

            Assert.Equal(idExistente, eventoPublicado!.CheckoutId);
            _repositoryMock.Verify(r => r.UpdateBasket(It.IsAny<ShoppingCart>()), Times.Never);
        }

        [Fact]
        public async Task Checkout_RepetidoAposFalhaNoDeleteDoCarrinho_PublicaOMesmoCheckoutId()
        {
            // Cenário do buraco que estamos fechando: o evento foi publicado, mas o
            // DeleteBasket falhou (Redis indisponível, processo morto...). O usuário
            // repete o checkout. O id precisa ser o mesmo, para o Ordering.API
            // reconhecer a repetição em vez de criar um segundo pedido.
            var armazenado = BuildCarrinhoParaCheckout(); // simula o que está no Redis
            var idsPublicados = new List<Guid>();

            _repositoryMock.Setup(r => r.GetBasket("usuario-teste")).ReturnsAsync(() => armazenado);
            _repositoryMock.Setup(r => r.UpdateBasket(It.IsAny<ShoppingCart>()))
                .Callback<ShoppingCart>(c => armazenado = c)
                .ReturnsAsync((ShoppingCart c) => c);
            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BasketCheckoutEvent, CancellationToken>((evt, _) => idsPublicados.Add(evt.CheckoutId))
                .Returns(Task.CompletedTask);

            // 1ª tentativa: publica, mas o DELETE falha.
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste"))
                .ThrowsAsync(new InvalidOperationException("redis indisponível"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" }));

            // 2ª tentativa: o DELETE agora funciona.
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste")).Returns(Task.CompletedTask);
            await _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" });

            Assert.Equal(2, idsPublicados.Count);
            Assert.Equal(idsPublicados[0], idsPublicados[1]);
        }
    }
}
