using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StatStock.Web.Models;

namespace StatStock.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Redirect to Manager Dashboard
        return RedirectToAction("Index", "Dashboard", new { area = "Manager" });
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
