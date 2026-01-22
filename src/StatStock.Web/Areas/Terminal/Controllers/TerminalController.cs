using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Web.Areas.Terminal.Models;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;

namespace StatStock.Web.Areas.Terminal.Controllers;

[Area("Terminal")]
public class TerminalController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TerminalController> _logger;

    public TerminalController(ApplicationDbContext context, ILogger<TerminalController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Terminal/Index (Quick Search)
    public async Task<IActionResult> Index(string? search = null)
    {
        var model = new ProductSearchViewModel
        {
            SearchQuery = search
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                model.Products = await _context.Products
                    .Where(p => p.SKU.ToLower().Contains(searchLower) || 
                                p.Name.ToLower().Contains(searchLower) ||
                                p.Category.ToLower().Contains(searchLower))
                    .OrderBy(p => p.Name)
                    .Take(20)
                    .ToListAsync();
            }
            else
            {
                // Show all products by default
                model.Products = await _context.Products
                    .OrderBy(p => p.Name)
                    .Take(20)
                    .ToListAsync();
            }
            
            model.TotalResults = model.Products.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading products");
            TempData["Error"] = "Error loading products. Please try again.";
        }

        return View(model);
    }

    // GET: Terminal/IncomingShipment
    public async Task<IActionResult> IncomingShipment(string? search = null, int? productId = null)
    {
        var model = new ShipmentFormViewModel();

        // If search query provided, fetch search results
        if (!string.IsNullOrWhiteSpace(search))
        {
            model.SearchQuery = search;
            var searchLower = search.ToLower();
            model.SearchResults = await _context.Products
                .Where(p => p.SKU.ToLower().Contains(searchLower) || 
                            p.Name.ToLower().Contains(searchLower))
                .OrderBy(p => p.Name)
                .Take(10)
                .ToListAsync();
        }

        // If product selected, load product details
        if (productId.HasValue)
        {
            model.SelectedProductId = productId;
            model.SelectedProduct = await _context.Products.FindAsync(productId.Value);
        }

        return View(model);
    }

    // POST: Terminal/IncomingShipment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IncomingShipment(int productId, int quantity, string? notes)
    {
        try
        {
            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                return RedirectToAction(nameof(IncomingShipment), new { productId });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(IncomingShipment));
            }

            // Update product stock
            product.StockQuantity += quantity;
            product.UpdatedAt = DateTime.UtcNow;

            // Create an order record for tracking
            var order = new Order
            {
                OrderNumber = $"IN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
                Type = OrderType.Incoming,
                Status = OrderStatus.Delivered, // Auto-approve incoming shipments
                CreatedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                Notes = notes ?? $"Terminal incoming shipment: {quantity} units",
                UserId = "terminal-user" // TODO: In production, use actual authenticated user ID
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order item
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            };

            _context.OrderItems.Add(orderItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Incoming shipment recorded: {OrderNumber}, Product: {ProductName}, Quantity: {Quantity}", 
                order.OrderNumber, product.Name, quantity);

            TempData["Success"] = $"✓ Incoming shipment recorded: {quantity} units of {product.Name}. New stock: {product.StockQuantity}";
            
            // Redirect back to incoming shipment form for next entry
            return RedirectToAction(nameof(IncomingShipment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording incoming shipment");
            TempData["Error"] = "Error recording shipment. Please try again.";
            return RedirectToAction(nameof(IncomingShipment), new { productId });
        }
    }

    // GET: Terminal/OutgoingShipment
    public async Task<IActionResult> OutgoingShipment(string? search = null, int? productId = null)
    {
        var model = new ShipmentFormViewModel();

        // If search query provided, fetch search results
        if (!string.IsNullOrWhiteSpace(search))
        {
            model.SearchQuery = search;
            var searchLower = search.ToLower();
            model.SearchResults = await _context.Products
                .Where(p => p.SKU.ToLower().Contains(searchLower) || 
                            p.Name.ToLower().Contains(searchLower))
                .OrderBy(p => p.Name)
                .Take(10)
                .ToListAsync();
        }

        // If product selected, load product details
        if (productId.HasValue)
        {
            model.SelectedProductId = productId;
            model.SelectedProduct = await _context.Products.FindAsync(productId.Value);
        }

        return View(model);
    }

    // POST: Terminal/OutgoingShipment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OutgoingShipment(int productId, int quantity, string? notes)
    {
        try
        {
            if (quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than 0.";
                return RedirectToAction(nameof(OutgoingShipment), new { productId });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(OutgoingShipment));
            }

            // Check if sufficient stock
            if (product.StockQuantity < quantity)
            {
                TempData["Error"] = $"Insufficient stock. Available: {product.StockQuantity} units.";
                return RedirectToAction(nameof(OutgoingShipment), new { productId });
            }

            // Update product stock
            product.StockQuantity -= quantity;
            product.UpdatedAt = DateTime.UtcNow;

            // Create an order record for tracking
            var order = new Order
            {
                OrderNumber = $"OUT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
                Type = OrderType.Outgoing,
                Status = OrderStatus.Delivered, // Auto-approve outgoing shipments
                CreatedAt = DateTime.UtcNow,
                ApprovedAt = DateTime.UtcNow,
                Notes = notes ?? $"Terminal outgoing shipment: {quantity} units",
                UserId = "terminal-user" // TODO: In production, use actual authenticated user ID
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order item
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            };

            _context.OrderItems.Add(orderItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Outgoing shipment recorded: {OrderNumber}, Product: {ProductName}, Quantity: {Quantity}", 
                order.OrderNumber, product.Name, quantity);

            TempData["Success"] = $"✓ Outgoing shipment recorded: {quantity} units of {product.Name}. Remaining stock: {product.StockQuantity}";
            
            // Redirect back to outgoing shipment form for next entry
            return RedirectToAction(nameof(OutgoingShipment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording outgoing shipment");
            TempData["Error"] = "Error recording shipment. Please try again.";
            return RedirectToAction(nameof(OutgoingShipment), new { productId });
        }
    }
}
