using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StatStock.Application.Interfaces;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Identity;
using StatStock.Web.Models;
using System.Security.Claims;

namespace StatStock.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationIdentityUser> _userManager;
    private readonly SignInManager<ApplicationIdentityUser> _signInManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationIdentityUser> userManager,
        SignInManager<ApplicationIdentityUser> signInManager,
        IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Find user by email
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Sign in with password (claims are added automatically via ApplicationUserClaimsPrincipalFactory)
        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? model.Email, 
            model.Password, 
            model.RememberMe, 
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in with role {Role}", model.Email, user.Role);
            
            // Log login event
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            await _auditService.LogAsync(user.Id, user.Email!, "Login", "Authentication", user.Id, 
                null, $"Role: {user.Role}", ipAddress);
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            
            // Redirect based on role
            return user.Role switch
            {
                UserRole.Admin or UserRole.Manager => RedirectToAction("Index", "Dashboard", new { area = "Manager" }),
                UserRole.FloorStaff => RedirectToAction("Index", "Terminal", new { area = "Terminal" }),
                _ => RedirectToAction("Index", "Home")
            };
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
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
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} created successfully", model.Email);
            
            // Sign in the user after successful registration
            await _signInManager.SignInAsync(user, isPersistent: false);
            
            return user.Role switch
            {
                UserRole.Admin or UserRole.Manager => RedirectToAction("Index", "Dashboard", new { area = "Manager" }),
                UserRole.FloorStaff => RedirectToAction("Index", "Terminal", new { area = "Terminal" }),
                _ => RedirectToAction("Index", "Home")
            };
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var userEmail = _userManager.GetUserName(User) ?? "Unknown";
        
        await _signInManager.SignOutAsync();
        
        // Log logout event
        if (!string.IsNullOrEmpty(userId))
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            await _auditService.LogAsync(userId, userEmail, "Logout", "Authentication", userId, 
                null, null, ipAddress);
        }
        
        _logger.LogInformation("User logged out");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
