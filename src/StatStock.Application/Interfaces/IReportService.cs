using StatStock.Application.DTOs;

namespace StatStock.Application.Interfaces;

public interface IReportService
{
    // Report generation methods
    Task<IEnumerable<StockMovementReportDto>> GetStockMovementReportAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<InventoryValuationReportDto>> GetInventoryValuationReportAsync();
    Task<IEnumerable<LowStockReportDto>> GetLowStockReportAsync();
    Task<IEnumerable<SalesTrendsReportDto>> GetSalesTrendsReportAsync(DateTime startDate, DateTime endDate);
    
    // Analytics methods
    Task<IEnumerable<DemandForecastDto>> GetDemandForecastAsync(int daysToForecast = 30);
    Task<IEnumerable<ReorderSuggestionDto>> GetReorderSuggestionsAsync();
    
    // Export methods
    Task<byte[]> ExportStockMovementToExcelAsync(DateTime startDate, DateTime endDate);
    Task<byte[]> ExportInventoryValuationToExcelAsync();
    Task<byte[]> ExportLowStockToExcelAsync();
    Task<byte[]> ExportSalesTrendsToExcelAsync(DateTime startDate, DateTime endDate);
    
    Task<byte[]> ExportStockMovementToPdfAsync(DateTime startDate, DateTime endDate);
    Task<byte[]> ExportInventoryValuationToPdfAsync();
    Task<byte[]> ExportLowStockToPdfAsync();
    Task<byte[]> ExportSalesTrendsToPdfAsync(DateTime startDate, DateTime endDate);
}
