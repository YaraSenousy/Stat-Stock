using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StatStock.Web.Models;

namespace StatStock.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("FloorStaff") || User.IsInRole("B2BClient"))
            {
                return RedirectToAction("Index", "Terminal", new { area = "Terminal" });
            }
            // Admin and Manager go to Manager Dashboard
            return RedirectToAction("Index", "Dashboard", new { area = "Manager" });
        }
        
        // Unauthenticated users go to Login instead of trying to access protected Manager Dashboard
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
