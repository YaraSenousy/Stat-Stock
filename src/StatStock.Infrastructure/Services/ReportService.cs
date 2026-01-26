using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StatStock.Application.DTOs;
using StatStock.Application.Interfaces;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace StatStock.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ApplicationDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
        
        // Configure QuestPDF license for community use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<IEnumerable<StockMovementReportDto>> GetStockMovementReportAsync(DateTime startDate, DateTime endDate)
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
            .ToListAsync();

        var productMovements = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.Product.Id, i.Product.Name, i.Product.SKU })
            .Select(g =>
            {
                var incoming = g.Where(i => i.Order.Type == OrderType.Incoming).Sum(i => i.Quantity);
                var outgoing = g.Where(i => i.Order.Type == OrderType.Outgoing).Sum(i => i.Quantity);
                var product = _context.Products.FirstOrDefault(p => p.Id == g.Key.Id);
                var currentStock = product?.StockQuantity ?? 0;
                var initialStock = currentStock - incoming + outgoing;

                return new StockMovementReportDto
                {
                    ProductId = g.Key.Id,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    InitialStock = initialStock,
                    IncomingQuantity = incoming,
                    OutgoingQuantity = outgoing,
                    FinalStock = currentStock,
                    TotalValue = currentStock * (product?.Price ?? 0)
                };
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        return productMovements;
    }

    public async Task<IEnumerable<InventoryValuationReportDto>> GetInventoryValuationReportAsync()
    {
        var products = await _context.Products
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return products.Select(p => new InventoryValuationReportDto
        {
            ProductId = p.Id,
            ProductName = p.Name,
            SKU = p.SKU,
            Category = p.Category,
            StockQuantity = p.StockQuantity,
            UnitPrice = p.Price,
            TotalValue = p.StockQuantity * p.Price,
            ReorderLevel = p.ReorderLevel,
            StockStatus = p.StockQuantity <= p.ReorderLevel ? "Low Stock" :
                         p.StockQuantity <= p.ReorderLevel * 2 ? "Warning" : "Good"
        });
    }

    public async Task<IEnumerable<LowStockReportDto>> GetLowStockReportAsync()
    {
        var lowStockProducts = await _context.Products
            .Where(p => p.StockQuantity <= p.ReorderLevel)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        var productIds = lowStockProducts.Select(p => p.Id).ToList();
        var recentOrders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Supplier)
            .Where(o => o.Items.Any(i => productIds.Contains(i.ProductId)))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return lowStockProducts.Select(p =>
        {
            var deficit = p.ReorderLevel - p.StockQuantity;
            var recommendedQty = Math.Max(deficit * 2, p.ReorderLevel); // Order double the deficit or reorder level
            var lastOrder = recentOrders
                .FirstOrDefault(o => o.Items.Any(i => i.ProductId == p.Id));

            return new LowStockReportDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                SKU = p.SKU,
                Category = p.Category,
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel,
                StockDeficit = deficit,
                UnitPrice = p.Price,
                SupplierName = lastOrder?.Supplier?.Name,
                RecommendedOrderQuantity = recommendedQty
            };
        });
    }

    public async Task<IEnumerable<SalesTrendsReportDto>> GetSalesTrendsReportAsync(DateTime startDate, DateTime endDate)
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
            .ToListAsync();

        var trends = orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new SalesTrendsReportDto
            {
                Date = g.Key,
                IncomingOrders = g.Count(o => o.Type == OrderType.Incoming),
                OutgoingOrders = g.Count(o => o.Type == OrderType.Outgoing),
                IncomingValue = g.Where(o => o.Type == OrderType.Incoming)
                    .SelectMany(o => o.Items)
                    .Sum(i => i.Quantity * i.UnitPrice),
                OutgoingValue = g.Where(o => o.Type == OrderType.Outgoing)
                    .SelectMany(o => o.Items)
                    .Sum(i => i.Quantity * i.UnitPrice),
                NetOrders = g.Count(o => o.Type == OrderType.Incoming) - g.Count(o => o.Type == OrderType.Outgoing),
                NetValue = g.Where(o => o.Type == OrderType.Incoming).SelectMany(o => o.Items).Sum(i => i.Quantity * i.UnitPrice) -
                          g.Where(o => o.Type == OrderType.Outgoing).SelectMany(o => o.Items).Sum(i => i.Quantity * i.UnitPrice)
            })
            .OrderBy(t => t.Date)
            .ToList();

        return trends;
    }

    public async Task<IEnumerable<DemandForecastDto>> GetDemandForecastAsync(int daysToForecast = 30)
    {
        var lookbackDays = 90; // Use last 90 days for forecast
        var startDate = DateTime.UtcNow.AddDays(-lookbackDays);

        var orders = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= startDate && o.Type == OrderType.Outgoing)
            .ToListAsync();

        var productDemands = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.Product.Id, i.Product.Name, i.Product.SKU })
            .Select(g =>
            {
                var product = _context.Products.FirstOrDefault(p => p.Id == g.Key.Id);
                var totalDemand = g.Sum(i => i.Quantity);
                var avgDailyDemand = (decimal)totalDemand / lookbackDays;
                var currentStock = product?.StockQuantity ?? 0;
                var daysUntilStockout = avgDailyDemand > 0 ? (int)(currentStock / avgDailyDemand) : 999;
                var confidence = g.Count() >= 5 ? 0.8m : 0.5m; // Higher confidence with more data points

                return new DemandForecastDto
                {
                    ProductId = g.Key.Id,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    CurrentStock = currentStock,
                    AverageDailyDemand = avgDailyDemand,
                    DaysUntilStockout = daysUntilStockout,
                    RecommendedOrderQuantity = (int)(avgDailyDemand * daysToForecast * 1.2m), // 20% buffer
                    SuggestedOrderDate = DateTime.UtcNow.AddDays(Math.Max(0, daysUntilStockout - 7)), // Order 7 days before stockout
                    Confidence = confidence
                };
            })
            .Where(f => f.AverageDailyDemand > 0)
            .OrderBy(f => f.DaysUntilStockout)
            .ToList();

        return productDemands;
    }

    public async Task<IEnumerable<ReorderSuggestionDto>> GetReorderSuggestionsAsync()
    {
        var products = await _context.Products
            .Where(p => p.StockQuantity <= p.ReorderLevel)
            .ToListAsync();

        var productIds = products.Select(p => p.Id).ToList();
        var recentOrders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Supplier)
            .Where(o => o.Items.Any(i => productIds.Contains(i.ProductId)))
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync();

        return products.Select(p =>
        {
            var deficit = p.ReorderLevel - p.StockQuantity;
            var recommendedQty = Math.Max(deficit * 2, p.ReorderLevel);
            var lastOrder = recentOrders
                .FirstOrDefault(o => o.Items.Any(i => i.ProductId == p.Id));

            var priority = p.StockQuantity == 0 ? "Critical" :
                          p.StockQuantity < p.ReorderLevel / 2 ? "High" : "Medium";

            var reason = p.StockQuantity == 0 ? "Out of stock" :
                        p.StockQuantity < p.ReorderLevel / 2 ? "Stock critically low" :
                        "Below reorder level";

            return new ReorderSuggestionDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                SKU = p.SKU,
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel,
                RecommendedQuantity = recommendedQty,
                EstimatedCost = recommendedQty * p.Price,
                Priority = priority,
                Reason = reason,
                SupplierId = lastOrder?.SupplierId,
                SupplierName = lastOrder?.Supplier?.Name
            };
        }).OrderByDescending(r => r.Priority == "Critical" ? 3 : r.Priority == "High" ? 2 : 1);
    }

    // Excel Export Methods
    public async Task<byte[]> ExportStockMovementToExcelAsync(DateTime startDate, DateTime endDate)
    {
        var data = await GetStockMovementReportAsync(startDate, endDate);
        
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Stock Movement");

        // Headers
        worksheet.Cell(1, 1).Value = "Product Name";
        worksheet.Cell(1, 2).Value = "SKU";
        worksheet.Cell(1, 3).Value = "Initial Stock";
        worksheet.Cell(1, 4).Value = "Incoming";
        worksheet.Cell(1, 5).Value = "Outgoing";
        worksheet.Cell(1, 6).Value = "Final Stock";
        worksheet.Cell(1, 7).Value = "Total Value";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        int row = 2;
        foreach (var item in data)
        {
            worksheet.Cell(row, 1).Value = item.ProductName;
            worksheet.Cell(row, 2).Value = item.SKU;
            worksheet.Cell(row, 3).Value = item.InitialStock;
            worksheet.Cell(row, 4).Value = item.IncomingQuantity;
            worksheet.Cell(row, 5).Value = item.OutgoingQuantity;
            worksheet.Cell(row, 6).Value = item.FinalStock;
            worksheet.Cell(row, 7).Value = item.TotalValue;
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportInventoryValuationToExcelAsync()
    {
        var data = await GetInventoryValuationReportAsync();
        
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Inventory Valuation");

        // Headers
        worksheet.Cell(1, 1).Value = "Product Name";
        worksheet.Cell(1, 2).Value = "SKU";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Stock Quantity";
        worksheet.Cell(1, 5).Value = "Unit Price";
        worksheet.Cell(1, 6).Value = "Total Value";
        worksheet.Cell(1, 7).Value = "Reorder Level";
        worksheet.Cell(1, 8).Value = "Status";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        int row = 2;
        foreach (var item in data)
        {
            worksheet.Cell(row, 1).Value = item.ProductName;
            worksheet.Cell(row, 2).Value = item.SKU;
            worksheet.Cell(row, 3).Value = item.Category;
            worksheet.Cell(row, 4).Value = item.StockQuantity;
            worksheet.Cell(row, 5).Value = item.UnitPrice;
            worksheet.Cell(row, 6).Value = item.TotalValue;
            worksheet.Cell(row, 7).Value = item.ReorderLevel;
            worksheet.Cell(row, 8).Value = item.StockStatus;
            
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "$#,##0.00";
            
            // Color code status
            if (item.StockStatus == "Low Stock")
                worksheet.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
            else if (item.StockStatus == "Warning")
                worksheet.Cell(row, 8).Style.Font.FontColor = XLColor.Orange;
            
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportLowStockToExcelAsync()
    {
        var data = await GetLowStockReportAsync();
        
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Low Stock Items");

        // Headers
        worksheet.Cell(1, 1).Value = "Product Name";
        worksheet.Cell(1, 2).Value = "SKU";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Current Stock";
        worksheet.Cell(1, 5).Value = "Reorder Level";
        worksheet.Cell(1, 6).Value = "Deficit";
        worksheet.Cell(1, 7).Value = "Recommended Order";
        worksheet.Cell(1, 8).Value = "Unit Price";
        worksheet.Cell(1, 9).Value = "Supplier";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        int row = 2;
        foreach (var item in data)
        {
            worksheet.Cell(row, 1).Value = item.ProductName;
            worksheet.Cell(row, 2).Value = item.SKU;
            worksheet.Cell(row, 3).Value = item.Category;
            worksheet.Cell(row, 4).Value = item.CurrentStock;
            worksheet.Cell(row, 5).Value = item.ReorderLevel;
            worksheet.Cell(row, 6).Value = item.StockDeficit;
            worksheet.Cell(row, 7).Value = item.RecommendedOrderQuantity;
            worksheet.Cell(row, 8).Value = item.UnitPrice;
            worksheet.Cell(row, 9).Value = item.SupplierName ?? "N/A";
            
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportSalesTrendsToExcelAsync(DateTime startDate, DateTime endDate)
    {
        var data = await GetSalesTrendsReportAsync(startDate, endDate);
        
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sales Trends");

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Incoming Orders";
        worksheet.Cell(1, 3).Value = "Outgoing Orders";
        worksheet.Cell(1, 4).Value = "Incoming Value";
        worksheet.Cell(1, 5).Value = "Outgoing Value";
        worksheet.Cell(1, 6).Value = "Net Orders";
        worksheet.Cell(1, 7).Value = "Net Value";

        // Style headers
        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        int row = 2;
        foreach (var item in data)
        {
            worksheet.Cell(row, 1).Value = item.Date.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = item.IncomingOrders;
            worksheet.Cell(row, 3).Value = item.OutgoingOrders;
            worksheet.Cell(row, 4).Value = item.IncomingValue;
            worksheet.Cell(row, 5).Value = item.OutgoingValue;
            worksheet.Cell(row, 6).Value = item.NetOrders;
            worksheet.Cell(row, 7).Value = item.NetValue;
            
            worksheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // PDF Export Methods
    public async Task<byte[]> ExportStockMovementToPdfAsync(DateTime startDate, DateTime endDate)
    {
        var data = await GetStockMovementReportAsync(startDate, endDate);
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.Header().Text($"Stock Movement Report ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})").FontSize(20).Bold();
                
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product Name").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("SKU").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Initial").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Incoming").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Outgoing").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Final").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Value").Bold();
                    });

                    foreach (var item in data)
                    {
                        table.Cell().BorderBottom(1).Padding(5).Text(item.ProductName);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.SKU);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.InitialStock.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.IncomingQuantity.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.OutgoingQuantity.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.FinalStock.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.TotalValue:N2}");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Bold();
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportInventoryValuationToPdfAsync()
    {
        var data = await GetInventoryValuationReportAsync();
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.Header().Text("Inventory Valuation Report").FontSize(20).Bold();
                
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("SKU").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Category").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Stock").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Unit Price").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total Value").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Status").Bold();
                    });

                    foreach (var item in data)
                    {
                        table.Cell().BorderBottom(1).Padding(5).Text(item.ProductName);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.SKU);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.Category);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.StockQuantity.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.UnitPrice:N2}");
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.TotalValue:N2}");
                        table.Cell().BorderBottom(1).Padding(5).Text(item.StockStatus);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Bold();
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportLowStockToPdfAsync()
    {
        var data = await GetLowStockReportAsync();
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.Header().Text("Low Stock Items Report").FontSize(20).Bold();
                
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("SKU").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Current").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Reorder Lvl").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Deficit").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Rec. Order").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Supplier").Bold();
                    });

                    foreach (var item in data)
                    {
                        table.Cell().BorderBottom(1).Padding(5).Text(item.ProductName);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.SKU);
                        table.Cell().BorderBottom(1).Padding(5).Text(item.CurrentStock.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.ReorderLevel.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.StockDeficit.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.RecommendedOrderQuantity.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.SupplierName ?? "N/A");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Bold();
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportSalesTrendsToPdfAsync(DateTime startDate, DateTime endDate)
    {
        var data = await GetSalesTrendsReportAsync(startDate, endDate);
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.Header().Text($"Sales Trends Report ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})").FontSize(20).Bold();
                
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Date").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("In Orders").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Out Orders").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("In Value").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Out Value").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Net Orders").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Net Value").Bold();
                    });

                    foreach (var item in data)
                    {
                        table.Cell().BorderBottom(1).Padding(5).Text(item.Date.ToString("yyyy-MM-dd"));
                        table.Cell().BorderBottom(1).Padding(5).Text(item.IncomingOrders.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text(item.OutgoingOrders.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.IncomingValue:N2}");
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.OutgoingValue:N2}");
                        table.Cell().BorderBottom(1).Padding(5).Text(item.NetOrders.ToString());
                        table.Cell().BorderBottom(1).Padding(5).Text($"${item.NetValue:N2}");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Bold();
                });
            });
        });

        return document.GeneratePdf();
    }
}
