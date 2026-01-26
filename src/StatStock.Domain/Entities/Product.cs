namespace StatStock.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int ReorderLevel { get; set; }
    public int StockQuantity { get; set; }
    public DateTime? ExpirationDate { get; set; }  // For tracking product shelf-life
    public bool TrackExpiration { get; set; }  // Flag to enable expiration tracking for this product
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
