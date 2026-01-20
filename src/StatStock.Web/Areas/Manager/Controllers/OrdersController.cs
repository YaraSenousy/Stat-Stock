using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Enums;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ApplicationDbContext context, ILogger<OrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? status = null, string? type = null)
    {
        try
        {
            var query = _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }

            // Filter by type
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<OrderType>(type, out var orderType))
            {
                query = query.Where(o => o.Type == orderType);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.OrderStatuses = Enum.GetNames<OrderStatus>();
            ViewBag.OrderTypes = Enum.GetNames<OrderType>();

            return View(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading orders");
            return View("Error");
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading order details");
            return View("Error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        try
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.Status = status;
            if (status == OrderStatus.Approved)
            {
                order.ApprovedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status");
            return View("Error");
        }
    }
}
