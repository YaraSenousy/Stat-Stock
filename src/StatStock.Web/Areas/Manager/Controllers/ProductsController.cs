using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? category = null, string? search = null, string? sortBy = null)
    {
        try
        {
            var query = _context.Products.AsQueryable();

            // Filter by category
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            // Search by SKU or Name
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.SKU.Contains(search) || p.Name.Contains(search));
            }

            // Sort
            query = sortBy switch
            {
                "name" => query.OrderBy(p => p.Name),
                "price" => query.OrderBy(p => p.Price),
                "stock" => query.OrderBy(p => p.StockQuantity),
                "category" => query.OrderBy(p => p.Category),
                _ => query.OrderBy(p => p.Name)
            };

            var products = await query.ToListAsync();
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSort = sortBy;

            return View(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading products");
            return View("Error");
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading product details");
            return View("Error");
        }
    }
}
