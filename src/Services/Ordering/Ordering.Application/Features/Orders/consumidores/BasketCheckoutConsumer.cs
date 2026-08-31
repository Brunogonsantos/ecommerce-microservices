using EventBus.Messages.Events;
using MassTransit;
using Ordering.Application.Contracts;
using Ordering.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Ordering.Application.Features.Orders.Consumers
{
    public class BasketCheckoutConsumer : IConsumer<BasketCheckoutEvent>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<BasketCheckoutConsumer> _logger;

        public BasketCheckoutConsumer(IOrderRepository orderRepository, ILogger<BasketCheckoutConsumer> logger)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Consume(ConsumeContext<BasketCheckoutEvent> context)
        {
            var message = context.Message;

            // Mapeia o evento recebido para a entidade de Domínio Order
            var order = new Order
            {
                UserName = message.UserName,
                TotalPrice = message.TotalPrice,
                FirstName = message.FirstName,
                LastName = message.LastName,
                EmailAddress = message.EmailAddress,
                AddressLine = message.AddressLine,
                Country = message.Country,
                State = message.State,
                ZipCode = message.ZipCode
            };

            await _orderRepository.CreateOrder(order);
            _logger.LogInformation("Pedido criado com sucesso para o usuário: {userName}", order.UserName);
        }
    }
}