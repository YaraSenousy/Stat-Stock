using StatStock.Domain.Enums;

namespace StatStock.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    
    // Foreign keys
    public int? SupplierId { get; set; }
    public string UserId { get; set; } = string.Empty;
    
    // Navigation properties
    public Supplier? Supplier { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
