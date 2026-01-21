using StatStock.Domain.Entities;

namespace StatStock.Web.Areas.Terminal.Models;

public class ShipmentFormViewModel
{
    public string? SearchQuery { get; set; }
    public List<Product> SearchResults { get; set; } = new();
    public int? SelectedProductId { get; set; }
    public Product? SelectedProduct { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}
