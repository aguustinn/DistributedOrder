using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StockReservation>(e => e.HasKey(r => r.Id));

        // Seed de produtos para demonstração
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), Name = "Notebook", StockQuantity = 50, Price = 4500m },
            new Product { Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), Name = "Mouse", StockQuantity = 200, Price = 120m },
            new Product { Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), Name = "Teclado", StockQuantity = 150, Price = 250m }
        );
    }
}
