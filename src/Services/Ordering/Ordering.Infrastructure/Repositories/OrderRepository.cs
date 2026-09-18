using Microsoft.EntityFrameworkCore;
using Ordering.Application.Contracts;
using Ordering.Domain.Entities;
using Ordering.Infrastructure.Data;

namespace Ordering.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderContext _dbContext;

        public OrderRepository(OrderContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<int> CreateOrder(Order order)
        {
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();
            return order.Id;
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserName(string userName)
        {
            return await _dbContext.Orders
                .Where(o => o.UserName == userName)
                .ToListAsync();
        }
    }
}