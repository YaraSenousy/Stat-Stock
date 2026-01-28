using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Services;
using StatStock.Web.Areas.Manager.Models;
using System.Security.Claims;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly ICustomUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ICustomUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // GET: Manager/Users
    public async Task<IActionResult> Index(string? searchTerm = null, UserRole? role = null)
    {
        var allUsers = await _userService.GetAllUsersAsync();
        var users = allUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            users = users.Where(u => 
                u.Email!.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                u.FirstName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || 
                u.LastName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (role.HasValue)
        {
            users = users.Where(u => u.Role == role.Value);
        }

        var model = users
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
            .ToList();

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

        var fullName = $"{model.FirstName} {model.LastName}";
        var result = await _userService.CreateUserAsync(model.Email, fullName, model.Password, model.Role.ToString());

        if (result.success)
        {
            _logger.LogInformation("Admin created user {Email}", model.Email);
            TempData["SuccessMessage"] = $"User '{model.Email}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.message ?? "Failed to create user.");
        return View(model);
    }

    // GET: Manager/Users/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
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

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Role = model.Role;
        user.Area = model.Area;

        var result = await _userService.UpdateUserAsync(user);

        if (result.success)
        {
            _logger.LogInformation("Admin updated user {Email}", user.Email);
            TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.message ?? "Failed to update user.");
        return View(model);
    }

    // GET: Manager/Users/ChangePassword/5
    public async Task<IActionResult> ChangePassword(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
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

        var user = await _userService.GetUserByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        // For admin password change, we bypass current password requirement
        // by setting the new password directly
        user.PasswordHash = HashPassword(model.NewPassword);
        var result = await _userService.UpdateUserAsync(user);

        if (result.success)
        {
            _logger.LogInformation("Admin changed password for user {Email}", user.Email);
            TempData["SuccessMessage"] = $"Password for '{user.Email}' changed successfully.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.message ?? "Failed to change password.");
        return View(model);
    }

    // GET: Manager/Users/Delete/5
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
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
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent deleting yourself
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userService.DeleteUserAsync(user.Id);

        if (result.success)
        {
            _logger.LogInformation("Admin deleted user {Email}", user.Email);
            TempData["SuccessMessage"] = $"User '{user.Email}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.message ?? "Failed to delete user.");
        return RedirectToAction(nameof(Delete), new { id });
    }

    // Helper method to hash password (duplicated from CustomUserService for admin password changes)
    private string HashPassword(string password)
    {
        const int SaltSize = 16;
        const int HashSize = 20;
        const int Iterations = 10000;

        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password, 
            salt, 
            Iterations, 
            System.Security.Cryptography.HashAlgorithmName.SHA256, 
            HashSize);

        byte[] hashWithSalt = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashWithSalt, 0, SaltSize);
        Array.Copy(hash, 0, hashWithSalt, SaltSize, HashSize);

        return Convert.ToBase64String(hashWithSalt);
    }
}
