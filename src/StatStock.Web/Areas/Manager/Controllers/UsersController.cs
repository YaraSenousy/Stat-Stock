using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Identity;
using StatStock.Web.Areas.Manager.Models;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationIdentityUser> _userManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationIdentityUser> userManager,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    // GET: Manager/Users
    public async Task<IActionResult> Index(string? searchTerm = null, UserRole? role = null)
    {
        var users = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            users = users.Where(u => 
                u.Email!.Contains(searchTerm) || 
                u.FirstName.Contains(searchTerm) || 
                u.LastName.Contains(searchTerm));
        }

        if (role.HasValue)
        {
            users = users.Where(u => u.Role == role.Value);
        }

        var model = await users
            .OrderBy(u => u.Email)
            .Select(u => new UserViewModel
            {
                Id = u.Id,
                Email = u.Email!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                Area = u.Area,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        ViewData["SearchTerm"] = searchTerm;
        ViewData["RoleFilter"] = role;

        return View(model);
    }

    // GET: Manager/Users/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Manager/Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationIdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Role = model.Role,
            Area = model.Area,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("Admin created user {Email}", model.Email);
            TempData["SuccessMessage"] = $"User '{model.Email}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: Manager/Users/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Area = user.Area
        };

        return View(model);
    }

    // POST: Manager/Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditUserViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Role = model.Role;
        user.Area = model.Area;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("Admin updated user {Email}", user.Email);
            TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: Manager/Users/ChangePassword/5
    public async Task<IActionResult> ChangePassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ChangePasswordViewModel
        {
            UserId = user.Id,
            Email = user.Email!
        };

        return View(model);
    }

    // POST: Manager/Users/ChangePassword/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        // Remove old password and set new one
        var removePasswordResult = await _userManager.RemovePasswordAsync(user);
        if (!removePasswordResult.Succeeded)
        {
            foreach (var error in removePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
        if (addPasswordResult.Succeeded)
        {
            _logger.LogInformation("Admin changed password for user {Email}", user.Email);
            TempData["SuccessMessage"] = $"Password for '{user.Email}' changed successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in addPasswordResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: Manager/Users/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new UserViewModel
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Area = user.Area,
            CreatedAt = user.CreatedAt
        };

        return View(model);
    }

    // POST: Manager/Users/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent deleting yourself
        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("Admin deleted user {Email}", user.Email);
            TempData["SuccessMessage"] = $"User '{user.Email}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToAction(nameof(Delete), new { id });
    }
}
