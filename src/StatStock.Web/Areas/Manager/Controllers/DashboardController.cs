using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Enums;
using StatStock.Web.Areas.Manager.Models;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var model = new DashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(),
                TotalOrders = await _context.Orders.CountAsync(),
                PendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
                LowStockProducts = await _context.Products.CountAsync(p => p.StockQuantity <= p.ReorderLevel),
                TotalStockValue = await _context.Products.SumAsync(p => p.Price * p.StockQuantity),
                RecentOrders = await _context.Orders
                    .Include(o => o.Supplier)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
                LowStockItems = await _context.Products
                    .Where(p => p.StockQuantity <= p.ReorderLevel)
                    .OrderBy(p => p.StockQuantity)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetInventoryStats()
    {
        try
        {
            var stats = await _context.Products
                .GroupBy(p => p.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count(),
                    TotalValue = g.Sum(p => p.Price * p.StockQuantity)
                })
                .ToListAsync();

            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory stats");
            return BadRequest();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetOrderTrends()
    {
        try
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var trends = await _context.Orders
                .Where(o => o.CreatedAt >= thirtyDaysAgo)
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Incoming = g.Count(o => o.Type == OrderType.Incoming),
                    Outgoing = g.Count(o => o.Type == OrderType.Outgoing)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Json(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order trends");
            return BadRequest();
        }
    }
}
