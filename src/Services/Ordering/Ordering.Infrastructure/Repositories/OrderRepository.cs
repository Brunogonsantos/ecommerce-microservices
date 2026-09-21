using Microsoft.Data.SqlClient;
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

        public Task<Order?> GetByCheckoutId(Guid checkoutId)
        {
            return _dbContext.Orders.FirstOrDefaultAsync(o => o.CheckoutId == checkoutId);
        }

        public async Task<int> CreateOrder(Order order)
        {
            _dbContext.Orders.Add(order);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Corrida: duas mensagens com o mesmo CheckoutId chegaram quase ao
                // mesmo tempo (ex: reentrega do RabbitMQ concorrente com o
                // processamento original) e ambas passaram pelo check-then-act do
                // consumer. O índice único no banco é quem garante a integridade
                // de verdade; aqui só recuperamos o pedido que já existe em vez de
                // propagar o erro como se fosse uma falha real.
                _dbContext.Entry(order).State = EntityState.Detached;
                var existing = await GetByCheckoutId(order.CheckoutId);
                return existing?.Id ?? throw new InvalidOperationException(
                    $"Violação de índice único para CheckoutId {order.CheckoutId}, mas o pedido não foi encontrado ao tentar recuperá-lo.", ex);
            }

            return order.Id;
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserName(string userName)
        {
            return await _dbContext.Orders
                .Where(o => o.UserName == userName)
                .ToListAsync();
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // 2601: Cannot insert duplicate key row (índice único)
            // 2627: Violation of UNIQUE KEY / PRIMARY KEY constraint
            return ex.InnerException is SqlException sqlEx &&
                   (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }
    }
}