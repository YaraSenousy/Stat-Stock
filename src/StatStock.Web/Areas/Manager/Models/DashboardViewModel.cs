using StatStock.Domain.Entities;

namespace StatStock.Web.Areas.Manager.Models;

public class DashboardViewModel
{
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int LowStockProducts { get; set; }
    public decimal TotalStockValue { get; set; }
    public List<Order> RecentOrders { get; set; } = new();
    public List<Product> LowStockItems { get; set; } = new();
}
