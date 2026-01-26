using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatStock.Application.Interfaces;

namespace StatStock.Web.Areas.Manager.Controllers;

[Area("Manager")]
[Authorize(Roles = "Admin,Manager")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    // Stock Movement Report
    public IActionResult StockMovement()
    {
        ViewBag.StartDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        ViewBag.EndDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetStockMovementData(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var data = await _reportService.GetStockMovementReportAsync(start, end);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock movement data");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportStockMovementExcel(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var bytes = await _reportService.ExportStockMovementToExcelAsync(start, end);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"StockMovement_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting stock movement to Excel");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(StockMovement));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportStockMovementPdf(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var bytes = await _reportService.ExportStockMovementToPdfAsync(start, end);
            return File(bytes, "application/pdf", $"StockMovement_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting stock movement to PDF");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(StockMovement));
        }
    }

    // Inventory Valuation Report
    public IActionResult InventoryValuation()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetInventoryValuationData()
    {
        try
        {
            var data = await _reportService.GetInventoryValuationReportAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory valuation data");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportInventoryValuationExcel()
    {
        try
        {
            var bytes = await _reportService.ExportInventoryValuationToExcelAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"InventoryValuation_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting inventory valuation to Excel");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(InventoryValuation));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportInventoryValuationPdf()
    {
        try
        {
            var bytes = await _reportService.ExportInventoryValuationToPdfAsync();
            return File(bytes, "application/pdf", $"InventoryValuation_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting inventory valuation to PDF");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(InventoryValuation));
        }
    }

    // Low Stock Report
    public IActionResult LowStock()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetLowStockData()
    {
        try
        {
            var data = await _reportService.GetLowStockReportAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low stock data");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportLowStockExcel()
    {
        try
        {
            var bytes = await _reportService.ExportLowStockToExcelAsync();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"LowStock_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting low stock to Excel");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(LowStock));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportLowStockPdf()
    {
        try
        {
            var bytes = await _reportService.ExportLowStockToPdfAsync();
            return File(bytes, "application/pdf", $"LowStock_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting low stock to PDF");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(LowStock));
        }
    }

    // Sales Trends Report
    public IActionResult SalesTrends()
    {
        ViewBag.StartDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        ViewBag.EndDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetSalesTrendsData(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var data = await _reportService.GetSalesTrendsReportAsync(start, end);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales trends data");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportSalesTrendsExcel(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var bytes = await _reportService.ExportSalesTrendsToExcelAsync(start, end);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"SalesTrends_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales trends to Excel");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(SalesTrends));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportSalesTrendsPdf(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var bytes = await _reportService.ExportSalesTrendsToPdfAsync(start, end);
            return File(bytes, "application/pdf", $"SalesTrends_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales trends to PDF");
            TempData["Error"] = "Failed to export report";
            return RedirectToAction(nameof(SalesTrends));
        }
    }

    // Analytics - Demand Forecast
    public IActionResult DemandForecast()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetDemandForecastData(int daysToForecast = 30)
    {
        try
        {
            var data = await _reportService.GetDemandForecastAsync(daysToForecast);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting demand forecast data");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Analytics - Reorder Suggestions
    public IActionResult ReorderSuggestions()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetReorderSuggestionsData()
    {
        try
        {
            var data = await _reportService.GetReorderSuggestionsAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reorder suggestions data");
            return Json(new { success = false, message = ex.Message });
        }
    }
}
