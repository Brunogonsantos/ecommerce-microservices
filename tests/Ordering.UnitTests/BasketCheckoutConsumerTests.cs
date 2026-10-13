using EventBus.Messages.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Ordering.Application.Contracts;
using Ordering.Application.Features.Orders.Consumers;
using Ordering.Domain.Entities;
using Xunit;

namespace Ordering.UnitTests
{
    public class BasketCheckoutConsumerTests
    {
        private static Mock<ConsumeContext<BasketCheckoutEvent>> BuildContext(BasketCheckoutEvent message)
        {
            var mockContext = new Mock<ConsumeContext<BasketCheckoutEvent>>();
            mockContext.Setup(c => c.Message).Returns(message);
            return mockContext;
        }

        [Fact]
        public async Task Consume_QuandoCheckoutIdNuncaFoiProcessado_CriaOPedido()
        {
            var checkoutId = Guid.NewGuid();
            var message = new BasketCheckoutEvent { CheckoutId = checkoutId, UserName = "usuario-teste", TotalPrice = 100m };

            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock.Setup(r => r.GetByCheckoutId(checkoutId)).ReturnsAsync((Order?)null);
            repositoryMock.Setup(r => r.CreateOrder(It.IsAny<Order>())).ReturnsAsync(1);

            var consumer = new BasketCheckoutConsumer(repositoryMock.Object, Mock.Of<ILogger<BasketCheckoutConsumer>>());

            await consumer.Consume(BuildContext(message).Object);

            // O ponto central do teste: CreateOrder TEM que ser chamado quando
            // é a primeira vez que esse CheckoutId aparece.
            repositoryMock.Verify(r => r.CreateOrder(It.Is<Order>(o => o.CheckoutId == checkoutId)), Times.Once);
        }

        [Fact]
        public async Task Consume_QuandoCheckoutIdJaFoiProcessado_NaoCriaPedidoDuplicado()
        {
            // Simula exatamente o cenário de reentrega do RabbitMQ/MassTransit:
            // a mesma mensagem chega de novo, com o mesmo CheckoutId.
            var checkoutId = Guid.NewGuid();
            var message = new BasketCheckoutEvent { CheckoutId = checkoutId, UserName = "usuario-teste", TotalPrice = 100m };

            var pedidoExistente = new Order { Id = 42, CheckoutId = checkoutId, UserName = "usuario-teste" };

            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock.Setup(r => r.GetByCheckoutId(checkoutId)).ReturnsAsync(pedidoExistente);

            var consumer = new BasketCheckoutConsumer(repositoryMock.Object, Mock.Of<ILogger<BasketCheckoutConsumer>>());

            await consumer.Consume(BuildContext(message).Object);

            // O ponto central do teste: CreateOrder NUNCA pode ser chamado
            // quando o CheckoutId já tem um pedido associado.
            repositoryMock.Verify(r => r.CreateOrder(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task Consume_MapeiaCorretamenteOsCamposDoEventoParaOPedido()
        {
            var checkoutId = Guid.NewGuid();
            var message = new BasketCheckoutEvent
            {
                CheckoutId = checkoutId,
                UserName = "carla",
                TotalPrice = 250.50m,
                FirstName = "Carla",
                LastName = "Souza",
                EmailAddress = "carla@teste.com",
                AddressLine = "Av. Teste, 456",
                Country = "Brasil",
                State = "SP",
                ZipCode = "01000-000"
            };

            Order? pedidoCriado = null;
            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock.Setup(r => r.GetByCheckoutId(checkoutId)).ReturnsAsync((Order?)null);
            repositoryMock.Setup(r => r.CreateOrder(It.IsAny<Order>()))
                .Callback<Order>(o => pedidoCriado = o)
                .ReturnsAsync(1);

            var consumer = new BasketCheckoutConsumer(repositoryMock.Object, Mock.Of<ILogger<BasketCheckoutConsumer>>());

            await consumer.Consume(BuildContext(message).Object);

            Assert.NotNull(pedidoCriado);
            Assert.Equal("carla", pedidoCriado!.UserName);
            Assert.Equal(250.50m, pedidoCriado.TotalPrice);
            Assert.Equal("carla@teste.com", pedidoCriado.EmailAddress);
            Assert.Equal(checkoutId, pedidoCriado.CheckoutId);
        }
    }
}
