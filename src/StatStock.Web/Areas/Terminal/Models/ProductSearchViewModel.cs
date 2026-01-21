using StatStock.Domain.Entities;

namespace StatStock.Web.Areas.Terminal.Models;

public class ProductSearchViewModel
{
    public string? SearchQuery { get; set; }
    public List<Product> Products { get; set; } = new();
    public int TotalResults { get; set; }
}
