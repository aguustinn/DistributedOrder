using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<List<Product>> GetProductsAsync();
    Task<(bool success, string? reason)> TryReserveStockAsync(Guid orderId, List<(Guid productId, int qty)> items);
}

public class InventoryAppService(
    InventoryDbContext db,
    ILogger<InventoryAppService> logger) : IInventoryService
{
    public async Task<List<Product>> GetProductsAsync() =>
        await db.Products.OrderBy(p => p.Name).ToListAsync();

    // Premissa: Consistência — usa transação para garantir atomicidade da reserva
    public async Task<(bool success, string? reason)> TryReserveStockAsync(
        Guid orderId,
        List<(Guid productId, int qty)> items)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var (productId, qty) in items)
            {
                // Bloqueia o registro para evitar race condition (concorrência)
                var product = await db.Products
                    .FromSqlRaw("SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", productId)
                    .FirstOrDefaultAsync();

                if (product is null)
                    return (false, $"Produto {productId} não encontrado");

                if (product.StockQuantity < qty)
                    return (false, $"Estoque insuficiente para '{product.Name}': disponível={product.StockQuantity}, solicitado={qty}");

                // Debita estoque e registra a reserva
                product.StockQuantity -= qty;
                product.UpdatedAt = DateTime.UtcNow;
                db.Reservations.Add(new StockReservation { OrderId = orderId, ProductId = productId, Quantity = qty });
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Estoque reservado com sucesso para pedido {OrderId}", orderId);
            return (true, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Erro ao reservar estoque para pedido {OrderId}", orderId);
            return (false, "Erro interno ao reservar estoque");
        }
    }
}
