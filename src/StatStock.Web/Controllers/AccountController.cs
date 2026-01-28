using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatStock.Application.Interfaces;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Services;
using StatStock.Web.Models;
using System.Security.Claims;

namespace StatStock.Web.Controllers;

public class AccountController : Controller
{
    private readonly ICustomUserService _userService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ICustomUserService userService,
        IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _userService = userService;
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

        // Authenticate user
        var user = await _userService.AuthenticateAsync(model.Email, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Create claims for the user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(24)
        };

        // Sign in the user
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

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
            UserRole.FloorStaff or UserRole.B2BClient => RedirectToAction("Index", "Terminal", new { area = "Terminal" }),
            _ => RedirectToAction("Index", "Home")
        };
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

        var fullName = $"{model.FirstName} {model.LastName}";
        var result = await _userService.CreateUserAsync(model.Email, fullName, model.Password, model.Role.ToString());

        if (result.success)
        {
            _logger.LogInformation("User {Email} created successfully", model.Email);
            
            // Authenticate and sign in the newly created user
            var user = await _userService.AuthenticateAsync(model.Email, model.Password);
            if (user != null)
            {
                // Create claims for the user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.GivenName, user.FirstName),
                    new Claim(ClaimTypes.Surname, user.LastName),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));
                
                return user.Role switch
                {
                    UserRole.Admin or UserRole.Manager => RedirectToAction("Index", "Dashboard", new { area = "Manager" }),
                    UserRole.FloorStaff or UserRole.B2BClient => RedirectToAction("Index", "Terminal", new { area = "Terminal" }),
                    _ => RedirectToAction("Index", "Home")
                };
            }
        }

        ModelState.AddModelError(string.Empty, result.message ?? "Failed to create user.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Unknown";
        
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
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
