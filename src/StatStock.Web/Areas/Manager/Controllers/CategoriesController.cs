using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ApplicationDbContext context, ILogger<CategoriesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Manager/Categories
    public async Task<IActionResult> Index()
    {
        try
        {
            // Get all distinct categories with product counts
            var categories = await _context.Products
                .GroupBy(p => p.Category)
                .Select(g => new CategoryViewModel
                {
                    Name = g.Key,
                    ProductCount = g.Count(),
                    LowStockCount = g.Count(p => p.StockQuantity <= p.ReorderLevel)
                })
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
            return View("Error");
        }
    }

    // POST: Manager/Categories/Rename
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(string oldName, string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                TempData["ErrorMessage"] = "Category names cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "New category name must be different from the old name.";
                return RedirectToAction(nameof(Index));
            }

            // Check if new name already exists
            var existingCategory = await _context.Products
                .AnyAsync(p => p.Category.ToLower() == newName.ToLower());
            
            if (existingCategory)
            {
                TempData["ErrorMessage"] = $"Category '{newName}' already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Update all products with the old category name
            var products = await _context.Products
                .Where(p => p.Category == oldName)
                .ToListAsync();

            foreach (var product in products)
            {
                product.Category = newName;
                product.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Category renamed from {OldName} to {NewName}", oldName, newName);
            TempData["SuccessMessage"] = $"Category '{oldName}' renamed to '{newName}' successfully! {products.Count} product(s) updated.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming category");
            TempData["ErrorMessage"] = "An error occurred while renaming the category.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Manager/Categories/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string name, string? moveToCategory = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Category name cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            var products = await _context.Products
                .Where(p => p.Category == name)
                .ToListAsync();

            if (!products.Any())
            {
                TempData["ErrorMessage"] = $"Category '{name}' not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(moveToCategory))
            {
                // Move products to another category
                foreach (var product in products)
                {
                    product.Category = moveToCategory;
                    product.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Category {Name} deleted, {Count} product(s) moved to {NewCategory}", 
                    name, products.Count, moveToCategory);
                TempData["SuccessMessage"] = $"Category '{name}' deleted successfully! {products.Count} product(s) moved to '{moveToCategory}'.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Cannot delete category '{name}'. Please move its {products.Count} product(s) to another category first.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category");
            TempData["ErrorMessage"] = "An error occurred while deleting the category.";
            return RedirectToAction(nameof(Index));
        }
    }
}

public class CategoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int LowStockCount { get; set; }
}
