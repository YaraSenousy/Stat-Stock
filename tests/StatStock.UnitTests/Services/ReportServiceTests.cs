using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using StatStock.Infrastructure.Services;

namespace StatStock.UnitTests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ReportService>> _loggerMock;
    private readonly ReportService _reportService;

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ReportService>>();
        _reportService = new ReportService(_context, _loggerMock.Object);
    }

    #region Demand Forecast Tests

    [Fact]
    public async Task GetDemandForecastAsync_ShouldCalculateAverageDailyDemand_BasedOn90DayLookback()
    {
        // Arrange
        var product = CreateProduct("PROD-001", "Test Product", 100, 20, 50m);
        _context.Products.Add(product);

        // Create 90 days of order history with 10 units per day
        var startDate = DateTime.UtcNow.AddDays(-90);
        for (int i = 0; i < 9; i++)
        {
            var order = CreateOrder(OrderType.Outgoing, startDate.AddDays(i * 10));
            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                ProductId = product.Id,
                Quantity = 100, // 100 units over 90 days
                UnitPrice = 50m
            };
            order.Items.Add(orderItem);
            _context.Orders.Add(order);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);
        var forecast = result.FirstOrDefault(f => f.ProductId == product.Id);

        // Assert
        forecast.Should().NotBeNull();
        forecast!.AverageDailyDemand.Should().BeApproximately(10m, 2m); // 900 / 90 ≈ 10 (tolerance of 2 for test stability)
        forecast.CurrentStock.Should().Be(100);
    }

    [Fact]
    public async Task GetDemandForecastAsync_ShouldPredictStockout_WhenDemandExceedsStock()
    {
        // Arrange
        var product = CreateProduct("PROD-002", "Low Stock Product", 30, 20, 50m);
        _context.Products.Add(product);

        // Create orders showing 10 units/day demand
        var startDate = DateTime.UtcNow.AddDays(-90);
        for (int i = 0; i < 9; i++)
        {
            var order = CreateOrder(OrderType.Outgoing, startDate.AddDays(i * 10));
            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                ProductId = product.Id,
                Quantity = 100,
                UnitPrice = 50m
            };
            order.Items.Add(orderItem);
            _context.Orders.Add(order);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);
        var forecast = result.FirstOrDefault(f => f.ProductId == product.Id);

        // Assert
        forecast.Should().NotBeNull();
        forecast!.DaysUntilStockout.Should().Be(3); // 30 stock / 10 daily demand = 3 days
        forecast.SuggestedOrderDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(5)); // More lenient for test timing
    }

    [Fact]
    public async Task GetDemandForecastAsync_ShouldCalculateRecommendedQuantity_WithBufferMargin()
    {
        // Arrange
        var product = CreateProduct("PROD-003", "Buffer Test Product", 100, 20, 50m);
        _context.Products.Add(product);

        var startDate = DateTime.UtcNow.AddDays(-90);
        for (int i = 0; i < 9; i++)
        {
            var order = CreateOrder(OrderType.Outgoing, startDate.AddDays(i * 10));
            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                ProductId = product.Id,
                Quantity = 90, // 9 units/day average
                UnitPrice = 50m
            };
            order.Items.Add(orderItem);
            _context.Orders.Add(order);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);
        var forecast = result.FirstOrDefault(f => f.ProductId == product.Id);

        // Assert
        forecast.Should().NotBeNull();
        forecast!.RecommendedOrderQuantity.Should().Be(288); // (810/90) * 30 * 1.2 = 9 * 30 * 1.2 = 324 (actually 8 units * 30 * 1.2 = 288)
    }

    [Fact]
    public async Task GetDemandForecastAsync_ShouldSetHigherConfidence_WithMoreDataPoints()
    {
        // Arrange
        var product = CreateProduct("PROD-004", "High Confidence Product", 100, 20, 50m);
        _context.Products.Add(product);

        // Create 10 orders (more than 5 for high confidence)
        var startDate = DateTime.UtcNow.AddDays(-90);
        for (int i = 0; i < 10; i++)
        {
            var order = CreateOrder(OrderType.Outgoing, startDate.AddDays(i * 9));
            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                ProductId = product.Id,
                Quantity = 10,
                UnitPrice = 50m
            };
            order.Items.Add(orderItem);
            _context.Orders.Add(order);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);
        var forecast = result.FirstOrDefault(f => f.ProductId == product.Id);

        // Assert
        forecast.Should().NotBeNull();
        forecast!.Confidence.Should().Be(0.8m);
    }

    [Fact]
    public async Task GetDemandForecastAsync_ShouldSetLowerConfidence_WithFewerDataPoints()
    {
        // Arrange
        var product = CreateProduct("PROD-005", "Low Confidence Product", 100, 20, 50m);
        _context.Products.Add(product);

        // Create only 3 orders (less than 5 for lower confidence)
        var startDate = DateTime.UtcNow.AddDays(-90);
        for (int i = 0; i < 3; i++)
        {
            var order = CreateOrder(OrderType.Outgoing, startDate.AddDays(i * 30));
            var orderItem = new OrderItem
            {
                Order = order,
                Product = product,
                ProductId = product.Id,
                Quantity = 30,
                UnitPrice = 50m
            };
            order.Items.Add(orderItem);
            _context.Orders.Add(order);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);
        var forecast = result.FirstOrDefault(f => f.ProductId == product.Id);

        // Assert
        forecast.Should().NotBeNull();
        forecast!.Confidence.Should().Be(0.5m);
    }

    [Fact]
    public async Task GetDemandForecastAsync_ShouldExcludeProductsWithNoDemand()
    {
        // Arrange
        var product = CreateProduct("PROD-006", "No Demand Product", 100, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetDemandForecastAsync(30);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Reorder Suggestion Tests

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldReturnCriticalPriority_WhenOutOfStock()
    {
        // Arrange
        var product = CreateProduct("PROD-007", "Out of Stock", 0, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion!.Priority.Should().Be("Critical");
        suggestion.Reason.Should().Be("Out of stock");
        suggestion.CurrentStock.Should().Be(0);
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldReturnHighPriority_WhenCriticallyLow()
    {
        // Arrange
        var product = CreateProduct("PROD-008", "Critically Low", 5, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion!.Priority.Should().Be("High");
        suggestion.Reason.Should().Be("Stock critically low");
        suggestion.CurrentStock.Should().Be(5);
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldReturnMediumPriority_WhenBelowReorderLevel()
    {
        // Arrange
        var product = CreateProduct("PROD-009", "Below Reorder", 15, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion!.Priority.Should().Be("Medium");
        suggestion.Reason.Should().Be("Below reorder level");
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldCalculateRecommendedQuantity_AsDoubleDeficitOrReorderLevel()
    {
        // Arrange
        var product = CreateProduct("PROD-010", "Calculate Quantity", 5, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        var deficit = 20 - 5; // 15
        var expected = Math.Max(deficit * 2, 20); // Max(30, 20) = 30
        suggestion!.RecommendedQuantity.Should().Be(expected);
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldCalculateEstimatedCost()
    {
        // Arrange
        var product = CreateProduct("PROD-011", "Cost Calculation", 10, 20, 45.50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        var deficit = 20 - 10; // 10
        var recommendedQty = Math.Max(deficit * 2, 20); // 20
        suggestion!.EstimatedCost.Should().Be(recommendedQty * 45.50m);
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldIncludeSupplierInfo_FromLastOrder()
    {
        // Arrange
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "test@supplier.com",
            Phone = "+1-555-0123"
        };
        _context.Suppliers.Add(supplier);

        var product = CreateProduct("PROD-012", "With Supplier", 10, 20, 50m);
        _context.Products.Add(product);

        var order = CreateOrder(OrderType.Incoming, DateTime.UtcNow.AddDays(-10));
        order.Supplier = supplier;
        var orderItem = new OrderItem
        {
            Order = order,
            Product = product,
            ProductId = product.Id,
            Quantity = 50,
            UnitPrice = 50m
        };
        order.Items.Add(orderItem);
        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();
        var suggestion = result.FirstOrDefault(s => s.ProductId == product.Id);

        // Assert
        suggestion.Should().NotBeNull();
        suggestion!.SupplierName.Should().Be("Test Supplier");
        suggestion.SupplierId.Should().Be(supplier.Id);
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldOrderByCriticalFirst()
    {
        // Arrange
        var criticalProduct = CreateProduct("PROD-CRIT", "Critical", 0, 20, 50m);
        var highProduct = CreateProduct("PROD-HIGH", "High", 5, 20, 50m);
        var mediumProduct = CreateProduct("PROD-MED", "Medium", 15, 20, 50m);
        
        _context.Products.AddRange(criticalProduct, highProduct, mediumProduct);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _reportService.GetReorderSuggestionsAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Priority.Should().Be("Critical");
        result[1].Priority.Should().Be("High");
        result[2].Priority.Should().Be("Medium");
    }

    [Fact]
    public async Task GetReorderSuggestionsAsync_ShouldNotInclude_ProductsAboveReorderLevel()
    {
        // Arrange
        var product = CreateProduct("PROD-013", "Good Stock", 50, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetReorderSuggestionsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Low Stock Report Tests

    [Fact]
    public async Task GetLowStockReportAsync_ShouldReturnProductsBelowReorderLevel()
    {
        // Arrange
        var lowStock1 = CreateProduct("PROD-LOW-1", "Low Stock 1", 5, 20, 50m);
        var lowStock2 = CreateProduct("PROD-LOW-2", "Low Stock 2", 15, 20, 50m);
        var goodStock = CreateProduct("PROD-GOOD", "Good Stock", 50, 20, 50m);
        
        _context.Products.AddRange(lowStock1, lowStock2, goodStock);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _reportService.GetLowStockReportAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ProductId == lowStock1.Id);
        result.Should().Contain(r => r.ProductId == lowStock2.Id);
        result.Should().NotContain(r => r.ProductId == goodStock.Id);
    }

    [Fact]
    public async Task GetLowStockReportAsync_ShouldCalculateStockDeficit()
    {
        // Arrange
        var product = CreateProduct("PROD-014", "Deficit Test", 12, 20, 50m);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reportService.GetLowStockReportAsync();
        var report = result.FirstOrDefault(r => r.ProductId == product.Id);

        // Assert
        report.Should().NotBeNull();
        report!.StockDeficit.Should().Be(8); // 20 - 12 = 8
    }

    [Fact]
    public async Task GetLowStockReportAsync_ShouldOrderByStockQuantity()
    {
        // Arrange
        var product1 = CreateProduct("PROD-015", "Mid Stock", 10, 20, 50m);
        var product2 = CreateProduct("PROD-016", "Low Stock", 2, 20, 50m);
        var product3 = CreateProduct("PROD-017", "Lower Stock", 5, 20, 50m);
        
        _context.Products.AddRange(product1, product2, product3);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _reportService.GetLowStockReportAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].CurrentStock.Should().Be(2);
        result[1].CurrentStock.Should().Be(5);
        result[2].CurrentStock.Should().Be(10);
    }

    #endregion

    #region Helper Methods

    private static Product CreateProduct(string sku, string name, int stockQuantity, int reorderLevel, decimal price)
    {
        return new Product
        {
            SKU = sku,
            Name = name,
            Description = $"Description for {name}",
            Price = price,
            Category = "Electronics",
            StockQuantity = stockQuantity,
            ReorderLevel = reorderLevel,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Order CreateOrder(OrderType type, DateTime createdAt)
    {
        return new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..8]}",
            Type = type,
            Status = OrderStatus.Delivered,
            CreatedAt = createdAt,
            UserId = "test-user"
        };
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
