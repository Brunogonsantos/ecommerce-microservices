using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Entities;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Repositories;
using Xunit;

namespace Ordering.UnitTests
{
    public class OrderRepositoryTests
    {
        // Cada teste usa um banco InMemory com nome único, para não vazar
        // estado de um teste para o outro.
        private static OrderContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<OrderContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new OrderContext(options);
        }

        private static Order BuildOrder(Guid checkoutId, string userName = "usuario-teste")
        {
            return new Order
            {
                CheckoutId = checkoutId,
                UserName = userName,
                TotalPrice = 100m,
                FirstName = "Fulano",
                LastName = "de Tal",
                EmailAddress = "fulano@teste.com",
                AddressLine = "Rua Teste, 123",
                Country = "Brasil",
                State = "RS",
                ZipCode = "90000-000"
            };
        }

        [Fact]
        public async Task CreateOrder_ComCheckoutIdNovo_CriaPedidoComSucesso()
        {
            await using var context = CreateContext();
            var repository = new OrderRepository(context);
            var checkoutId = Guid.NewGuid();

            var orderId = await repository.CreateOrder(BuildOrder(checkoutId));

            Assert.True(orderId > 0);
            Assert.Single(context.Orders);
        }

        [Fact]
        public async Task GetByCheckoutId_QuandoNaoExiste_RetornaNull()
        {
            await using var context = CreateContext();
            var repository = new OrderRepository(context);

            var resultado = await repository.GetByCheckoutId(Guid.NewGuid());

            Assert.Null(resultado);
        }

        [Fact]
        public async Task GetByCheckoutId_QuandoExiste_RetornaOPedidoCorreto()
        {
            await using var context = CreateContext();
            var repository = new OrderRepository(context);
            var checkoutId = Guid.NewGuid();
            await repository.CreateOrder(BuildOrder(checkoutId, "maria"));

            var resultado = await repository.GetByCheckoutId(checkoutId);

            Assert.NotNull(resultado);
            Assert.Equal("maria", resultado!.UserName);
        }

        [Fact]
        public void Modelo_PossuiIndiceUnicoEmCheckoutId()
        {
            // O provider InMemory NÃO aplica índices únicos, então não dá para provar
            // a rejeição de duplicatas aqui. O que dá para garantir em teste de unidade
            // é que o índice único está declarado no modelo; a rejeição de verdade
            // (erro 2601/2627 do SQL Server) deve ser coberta por um teste de
            // integração com SQL Server real.
            using var context = CreateContext();
            var entityType = context.Model.FindEntityType(typeof(Order));

            Assert.NotNull(entityType);
            var indice = entityType!.GetIndexes()
                .SingleOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Order.CheckoutId));

            Assert.NotNull(indice);
            Assert.True(indice!.IsUnique);
        }

        [Fact]
        public async Task GetOrdersByUserName_RetornaApenasPedidosDoUsuarioInformado()
        {
            await using var context = CreateContext();
            var repository = new OrderRepository(context);

            await repository.CreateOrder(BuildOrder(Guid.NewGuid(), "ana"));
            await repository.CreateOrder(BuildOrder(Guid.NewGuid(), "ana"));
            await repository.CreateOrder(BuildOrder(Guid.NewGuid(), "bruno"));

            var pedidosDeAna = (await repository.GetOrdersByUserName("ana")).ToList();

            Assert.Equal(2, pedidosDeAna.Count);
            Assert.All(pedidosDeAna, p => Assert.Equal("ana", p.UserName));
        }
    }
}
