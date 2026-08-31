using Ordering.Domain.Entities;

namespace Ordering.Application.Contracts
{
    public interface IOrderRepository
    {
        Task<IEnumerable<Order>> GetOrdersByUserName(string userName);
        Task<int> CreateOrder(Order order);
    }
}