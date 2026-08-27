using Catalog.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data
{
    public class CatalogContext : DbContext
    {
        public CatalogContext(DbContextOptions<CatalogContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)"); // Define o tipo de coluna diretamente no SQL Server

            // OU se preferir gerenciar apenas a precisão pelo EF Core:
            // modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        }
    }
}
