using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ordering.API.Controllers;
using Ordering.Application.Contracts;
using Ordering.Domain.Entities;
using System.Security.Claims;
using Xunit;

namespace Ordering.UnitTests
{
    public class OrderControllerTests
    {
        private readonly Mock<IOrderRepository> _repositoryMock = new();

        private OrderController CriarController(ClaimsPrincipal usuario)
        {
            return new OrderController(_repositoryMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = usuario }
                }
            };
        }

        private static ClaimsPrincipal Autenticado(string nome, string papel)
        {
            var identidade = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, nome), new Claim(ClaimTypes.Role, papel) },
                authenticationType: "teste");
            return new ClaimsPrincipal(identidade);
        }

        [Fact]
        public async Task GetOrdersByUserName_QuandoUsuarioPedeOsProprios_RetornaOk()
        {
            _repositoryMock
                .Setup(r => r.GetOrdersByUserName("cliente"))
                .ReturnsAsync(new List<Order> { new() { Id = 1, UserName = "cliente" } });

            var controller = CriarController(Autenticado("cliente", "Customer"));

            var resultado = await controller.GetOrdersByUserName("cliente");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<Order>>(ok.Value));
        }

        [Fact]
        public async Task GetOrdersByUserName_QuandoClientePedePedidosDeOutro_RetornaForbid_SemConsultarOBanco()
        {
            var controller = CriarController(Autenticado("cliente", "Customer"));

            var resultado = await controller.GetOrdersByUserName("outra-pessoa");

            Assert.IsType<ForbidResult>(resultado.Result);
            _repositoryMock.Verify(r => r.GetOrdersByUserName(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetOrdersByUserName_QuandoAdminPedePedidosDeOutro_RetornaOk()
        {
            _repositoryMock
                .Setup(r => r.GetOrdersByUserName("outra-pessoa"))
                .ReturnsAsync(new List<Order>());

            var controller = CriarController(Autenticado("admin", "Admin"));

            var resultado = await controller.GetOrdersByUserName("outra-pessoa");

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task GetOrdersByUserName_QuandoNaoHaIdentidadeNoToken_RetornaForbid()
        {
            // Token sem claim de nome nem de papel: não dá para provar quem é o dono.
            var controller = CriarController(new ClaimsPrincipal(new ClaimsIdentity()));

            var resultado = await controller.GetOrdersByUserName("cliente");

            Assert.IsType<ForbidResult>(resultado.Result);
            _repositoryMock.Verify(r => r.GetOrdersByUserName(It.IsAny<string>()), Times.Never);
        }
    }
}
