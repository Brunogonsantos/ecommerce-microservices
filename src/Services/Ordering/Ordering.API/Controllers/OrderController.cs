using Microsoft.AspNetCore.Mvc;
using Ordering.Application.Contracts;
using Ordering.Domain.Entities;
using System.Net;

namespace Ordering.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;

        public OrderController(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        }

        [HttpGet("{userName}", Name = "GetOrderByName")]
        [ProducesResponseType(typeof(IEnumerable<Order>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByUserName(string userName)
        {
            var orders = await _orderRepository.GetOrdersByUserName(userName);
            return Ok(orders);
        }
    }
}