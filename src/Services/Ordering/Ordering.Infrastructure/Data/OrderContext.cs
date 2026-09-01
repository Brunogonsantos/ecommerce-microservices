using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Data
{
    public class OrderContext : DbContext
    {
        public OrderContext(DbContextOptions<OrderContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Define precisão para campos decimais (18 dígitos no total, 2 casas decimais)
            modelBuilder.Entity<Order>()
                .Property(p => p.TotalPrice)
                .HasColumnType("decimal(18,2)");
        }
    }
}