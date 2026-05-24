namespace InventoryService.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
}
