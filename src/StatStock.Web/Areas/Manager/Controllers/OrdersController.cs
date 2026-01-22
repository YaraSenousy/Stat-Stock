using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Enums;
using StatStock.Domain.Entities;

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

    public async Task<IActionResult> Index(string? status = null, string? type = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
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

            // Search by order number
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.OrderNumber.Contains(search));
            }

            // Filter by date range
            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                // Include the entire day for toDate
                var endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.CreatedAt <= endDate);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            // Apply automated approval rules
            await ApplyAutomatedApprovalRules(orders);

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = type;
            ViewBag.CurrentSearch = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
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

            // Add notification
            TempData["SuccessMessage"] = $"Order {order.OrderNumber} status updated to {status}.";

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status");
            TempData["ErrorMessage"] = "Failed to update order status. Please try again.";
            return View("Error");
        }
    }

    [HttpPost]
    public async Task<IActionResult> BulkUpdateStatus(int[] orderIds, OrderStatus status)
    {
        try
        {
            if (orderIds == null || orderIds.Length == 0)
            {
                TempData["ErrorMessage"] = "No orders selected. Please select at least one order.";
                return RedirectToAction(nameof(Index));
            }

            var orders = await _context.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            if (!orders.Any())
            {
                TempData["ErrorMessage"] = "Selected orders not found.";
                return RedirectToAction(nameof(Index));
            }

            int updatedCount = 0;
            foreach (var order in orders)
            {
                // Only update if order is in a valid state for the transition
                if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Approved)
                {
                    order.Status = status;
                    if (status == OrderStatus.Approved)
                    {
                        order.ApprovedAt = DateTime.UtcNow;
                    }
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully updated {updatedCount} order(s) to {status}.";
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating order status");
            TempData["ErrorMessage"] = "Failed to update orders. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Applies automated approval rules to pending orders.
    /// Rules:
    /// 1. Auto-approve incoming orders with total value < $500
    /// 2. Auto-approve orders from trusted suppliers (configurable list)
    /// 3. Auto-approve terminal-generated orders (already delivered)
    /// </summary>
    private async Task ApplyAutomatedApprovalRules(IEnumerable<Order> orders)
    {
        try
        {
            var pendingOrders = orders.Where(o => o.Status == OrderStatus.Pending).ToList();
            
            if (!pendingOrders.Any())
                return;

            // Trusted suppliers (in a real app, this would come from configuration)
            var trustedSupplierNames = new[] { "TechWholesale Inc.", "Office Depot Pro" };

            bool hasChanges = false;

            foreach (var order in pendingOrders)
            {
                bool shouldAutoApprove = false;
                string reason = "";

                // Rule 1: Auto-approve incoming orders with total value < $500
                var totalValue = order.Items.Sum(i => i.Quantity * i.UnitPrice);
                if (order.Type == OrderType.Incoming && totalValue < 500)
                {
                    shouldAutoApprove = true;
                    reason = $"Auto-approved: Incoming order under $500 (Total: ${totalValue:N2})";
                }

                // Rule 2: Auto-approve orders from trusted suppliers
                if (!shouldAutoApprove && order.Supplier != null && trustedSupplierNames.Contains(order.Supplier.Name))
                {
                    shouldAutoApprove = true;
                    reason = $"Auto-approved: Trusted supplier ({order.Supplier.Name})";
                }

                // Rule 3: Terminal orders are already delivered, should be auto-approved
                if (!shouldAutoApprove && order.Status == OrderStatus.Delivered)
                {
                    shouldAutoApprove = true;
                    reason = "Auto-approved: Terminal-generated order";
                }

                if (shouldAutoApprove)
                {
                    order.Status = OrderStatus.Approved;
                    order.ApprovedAt = DateTime.UtcNow;
                    
                    // Add note about auto-approval
                    if (!string.IsNullOrEmpty(order.Notes))
                    {
                        order.Notes += $"\n{reason}";
                    }
                    else
                    {
                        order.Notes = reason;
                    }

                    hasChanges = true;
                    _logger.LogInformation($"Order {order.OrderNumber} auto-approved. Reason: {reason}");
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying automated approval rules");
            // Don't throw - this is a background process that shouldn't break the page load
        }
    }
}
