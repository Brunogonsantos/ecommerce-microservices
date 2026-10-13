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
        public async Task Checkout_GeraCheckoutIdUnico_EPublicaOEvento()
        {
            var carrinho = new ShoppingCart("usuario-teste");
            carrinho.Items.Add(new ShoppingCartItem { ProductId = "p1", ProductName = "Produto", Price = 50m, Quantity = 2 });

            _repositoryMock.Setup(r => r.GetBasket("usuario-teste")).ReturnsAsync(carrinho);
            _repositoryMock.Setup(r => r.DeleteBasket("usuario-teste")).Returns(Task.CompletedTask);

            BasketCheckoutEvent? eventoPublicado = null;
            _publishEndpointMock
                .Setup(p => p.Publish(It.IsAny<BasketCheckoutEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BasketCheckoutEvent, CancellationToken>((evt, _) => eventoPublicado = evt)
                .Returns(Task.CompletedTask);

            var resultado = await _controller.Checkout(new BasketCheckout { UserName = "usuario-teste" });

            Assert.IsType<AcceptedResult>(resultado);
            Assert.NotNull(eventoPublicado);
            Assert.NotEqual(Guid.Empty, eventoPublicado!.CheckoutId); // o ponto central do teste de idempotência: sempre gera um CheckoutId novo e não-vazio
            _repositoryMock.Verify(r => r.DeleteBasket("usuario-teste"), Times.Once);
        }
    }
}
