using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Domain.Entities;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(ApplicationDbContext context, ILogger<SuppliersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: Manager/Suppliers
    public async Task<IActionResult> Index(string? search = null)
    {
        try
        {
            var query = _context.Suppliers.AsQueryable();

            // Search by name, email, or phone
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.Name.Contains(search) || 
                                        s.Email.Contains(search) || 
                                        s.Phone.Contains(search));
            }

            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();

            ViewBag.CurrentSearch = search;

            return View(suppliers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading suppliers");
            return View("Error");
        }
    }

    // GET: Manager/Suppliers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Orders)
                .FirstOrDefaultAsync(s => s.Id == id);
            
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading supplier details");
            return View("Error");
        }
    }

    // GET: Manager/Suppliers/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Manager/Suppliers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        try
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingEmail = await _context.Suppliers.AnyAsync(s => s.Email == supplier.Email);
                if (existingEmail)
                {
                    ModelState.AddModelError("Email", "A supplier with this email already exists.");
                    return View(supplier);
                }

                supplier.CreatedAt = DateTime.UtcNow;
                supplier.UpdatedAt = DateTime.UtcNow;
                
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Supplier {Name} created successfully", supplier.Name);
                TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' created successfully!";
                
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier");
            ModelState.AddModelError("", "An error occurred while creating the supplier.");
            return View(supplier);
        }
    }

    // GET: Manager/Suppliers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading edit supplier form");
            return View("Error");
        }
    }

    // POST: Manager/Suppliers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.Id)
        {
            return NotFound();
        }

        try
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists for a different supplier
                var existingEmail = await _context.Suppliers
                    .AnyAsync(s => s.Email == supplier.Email && s.Id != supplier.Id);
                if (existingEmail)
                {
                    ModelState.AddModelError("Email", "A supplier with this email already exists.");
                    return View(supplier);
                }

                supplier.UpdatedAt = DateTime.UtcNow;
                
                _context.Update(supplier);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Supplier {Name} updated successfully", supplier.Name);
                TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' updated successfully!";
                
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await SupplierExists(supplier.Id))
            {
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier");
            ModelState.AddModelError("", "An error occurred while updating the supplier.");
            return View(supplier);
        }
    }

    // GET: Manager/Suppliers/Delete/5
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Orders)
                .FirstOrDefaultAsync(s => s.Id == id);
            
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delete supplier confirmation");
            return View("Error");
        }
    }

    // POST: Manager/Suppliers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Supplier {Name} deleted successfully", supplier.Name);
            TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' deleted successfully!";
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier");
            TempData["ErrorMessage"] = "An error occurred while deleting the supplier. It may be referenced by existing orders.";
            return RedirectToAction(nameof(Delete), new { id });
        }
    }

    private async Task<bool> SupplierExists(int id)
    {
        return await _context.Suppliers.AnyAsync(e => e.Id == id);
    }
}
