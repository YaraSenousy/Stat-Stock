namespace StatStock.Application.DTOs;

public class StockMovementReportDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int InitialStock { get; set; }
    public int IncomingQuantity { get; set; }
    public int OutgoingQuantity { get; set; }
    public int FinalStock { get; set; }
    public decimal TotalValue { get; set; }
}

public class InventoryValuationReportDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public int ReorderLevel { get; set; }
    public string StockStatus { get; set; } = string.Empty;
}

public class LowStockReportDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
    public int StockDeficit { get; set; }
    public decimal UnitPrice { get; set; }
    public string? SupplierName { get; set; }
    public int RecommendedOrderQuantity { get; set; }
}

public class SalesTrendsReportDto
{
    public DateTime Date { get; set; }
    public int IncomingOrders { get; set; }
    public int OutgoingOrders { get; set; }
    public decimal IncomingValue { get; set; }
    public decimal OutgoingValue { get; set; }
    public int NetOrders { get; set; }
    public decimal NetValue { get; set; }
}

public class DemandForecastDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public decimal AverageDailyDemand { get; set; }
    public int DaysUntilStockout { get; set; }
    public int RecommendedOrderQuantity { get; set; }
    public DateTime SuggestedOrderDate { get; set; }
    public decimal Confidence { get; set; }
}

public class ReorderSuggestionDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
    public int RecommendedQuantity { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
}
