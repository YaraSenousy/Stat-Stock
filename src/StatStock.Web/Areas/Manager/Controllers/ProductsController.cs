using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Entities;
using System.Text;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "Admin,Manager")]
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

    // GET: Manager/Products/Export
    public async Task<IActionResult> Export()
    {
        try
        {
            var products = await _context.Products
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var csv = new StringBuilder();
            // Add CSV header
            csv.AppendLine("SKU,Name,Description,Category,Price,StockQuantity,ReorderLevel");

            // Add data rows
            foreach (var product in products)
            {
                csv.AppendLine($"\"{product.SKU}\",\"{EscapeCsv(product.Name)}\",\"{EscapeCsv(product.Description)}\",\"{EscapeCsv(product.Category)}\",{product.Price},{product.StockQuantity},{product.ReorderLevel}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"products_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            _logger.LogInformation("Products exported to CSV. {Count} products exported.", products.Count);

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting products");
            TempData["ErrorMessage"] = "An error occurred while exporting products.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET: Manager/Products/Import
    public IActionResult Import()
    {
        return View();
    }

    // POST: Manager/Products/Import
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file to import.");
                return View();
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only CSV files are supported.");
                return View();
            }

            var importedCount = 0;
            var updatedCount = 0;
            var errorCount = 0;
            var errors = new List<string>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                // Skip header
                await reader.ReadLineAsync();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var values = ParseCsvLine(line);
                        
                        if (values.Length < 7)
                        {
                            errors.Add($"Invalid row format: {line}");
                            errorCount++;
                            continue;
                        }

                        var sku = values[0].Trim();
                        var name = values[1].Trim();
                        var description = values[2].Trim();
                        var category = values[3].Trim();
                        
                        if (!decimal.TryParse(values[4], out var price) || price < 0)
                        {
                            errors.Add($"Invalid price for SKU {sku}: {values[4]}");
                            errorCount++;
                            continue;
                        }
                        
                        if (!int.TryParse(values[5], out var stockQuantity) || stockQuantity < 0)
                        {
                            errors.Add($"Invalid stock quantity for SKU {sku}: {values[5]}");
                            errorCount++;
                            continue;
                        }
                        
                        if (!int.TryParse(values[6], out var reorderLevel) || reorderLevel < 0)
                        {
                            errors.Add($"Invalid reorder level for SKU {sku}: {values[6]}");
                            errorCount++;
                            continue;
                        }

                        // Check if product exists
                        var existingProduct = await _context.Products
                            .FirstOrDefaultAsync(p => p.SKU == sku);

                        if (existingProduct != null)
                        {
                            // Update existing product
                            existingProduct.Name = name;
                            existingProduct.Description = description;
                            existingProduct.Category = category;
                            existingProduct.Price = price;
                            existingProduct.StockQuantity = stockQuantity;
                            existingProduct.ReorderLevel = reorderLevel;
                            existingProduct.UpdatedAt = DateTime.UtcNow;
                            updatedCount++;
                        }
                        else
                        {
                            // Create new product
                            var product = new Product
                            {
                                SKU = sku,
                                Name = name,
                                Description = description,
                                Category = category,
                                Price = price,
                                StockQuantity = stockQuantity,
                                ReorderLevel = reorderLevel,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _context.Products.Add(product);
                            importedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing row: {line}. {ex.Message}");
                        errorCount++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Products imported. {ImportedCount} new, {UpdatedCount} updated, {ErrorCount} errors.",
                importedCount, updatedCount, errorCount);

            var message = $"Import completed! {importedCount} new product(s) created, {updatedCount} product(s) updated.";
            if (errorCount > 0)
            {
                message += $" {errorCount} error(s) occurred.";
                ViewBag.ImportErrors = errors;
            }
            
            TempData["SuccessMessage"] = message;
            
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing products");
            ModelState.AddModelError("", "An error occurred while importing products: " + ex.Message);
            return View();
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return value.Replace("\"", "\"\"");
    }

    private string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(line[i]);
            }
        }
        
        values.Add(current.ToString());
        return values.ToArray();
    }
}
