using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Entities;

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

    // GET: Manager/Products/Create
    public async Task<IActionResult> Create()
    {
        try
        {
            // Get existing categories for dropdown
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            
            ViewBag.Categories = categories;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading create product form");
            return View("Error");
        }
    }

    // POST: Manager/Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        try
        {
            if (ModelState.IsValid)
            {
                // Check if SKU already exists
                var existingSku = await _context.Products.AnyAsync(p => p.SKU == product.SKU);
                if (existingSku)
                {
                    ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                    var categories = await _context.Products
                        .Select(p => p.Category)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();
                    ViewBag.Categories = categories;
                    return View(product);
                }

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;
                
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Product {SKU} created successfully", product.SKU);
                TempData["SuccessMessage"] = $"Product '{product.Name}' created successfully!";
                
                return RedirectToAction(nameof(Index));
            }

            var cats = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            ViewBag.Categories = cats;
            return View(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            ModelState.AddModelError("", "An error occurred while creating the product.");
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            ViewBag.Categories = categories;
            return View(product);
        }
    }

    // GET: Manager/Products/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            ViewBag.Categories = categories;

            return View(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit product form");
            return View("Error");
        }
    }

    // POST: Manager/Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return NotFound();
        }

        try
        {
            if (ModelState.IsValid)
            {
                // Check if SKU already exists for a different product
                var existingSku = await _context.Products
                    .AnyAsync(p => p.SKU == product.SKU && p.Id != product.Id);
                if (existingSku)
                {
                    ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                    var categories = await _context.Products
                        .Select(p => p.Category)
                        .Distinct()
                        .OrderBy(c => c)
                        .ToListAsync();
                    ViewBag.Categories = categories;
                    return View(product);
                }

                product.UpdatedAt = DateTime.UtcNow;
                
                _context.Update(product);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Product {SKU} updated successfully", product.SKU);
                TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully!";
                
                return RedirectToAction(nameof(Index));
            }

            var cats = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            ViewBag.Categories = cats;
            return View(product);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ProductExists(product.Id))
            {
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product");
            ModelState.AddModelError("", "An error occurred while updating the product.");
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
            ViewBag.Categories = categories;
            return View(product);
        }
    }

    // GET: Manager/Products/Delete/5
    public async Task<IActionResult> Delete(int id)
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
            _logger.LogError(ex, "Error loading delete product confirmation");
            return View("Error");
        }
    }

    // POST: Manager/Products/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Product {SKU} deleted successfully", product.SKU);
            TempData["SuccessMessage"] = $"Product '{product.Name}' deleted successfully!";
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product");
            TempData["ErrorMessage"] = "An error occurred while deleting the product. It may be referenced by existing orders.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task<bool> ProductExists(int id)
    {
        return await _context.Products.AnyAsync(e => e.Id == id);
    }
}
