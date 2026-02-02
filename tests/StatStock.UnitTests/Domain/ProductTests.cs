using FluentAssertions;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using Xunit;

namespace StatStock.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Product_ShouldBeCreated_WithValidProperties()
    {
        // Arrange & Act
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Test Product",
            Description = "Test Description",
            Category = "Electronics",
            Price = 99.99m,
            StockQuantity = 100,
            ReorderLevel = 20
        };

        // Assert
        product.SKU.Should().Be("PROD-001");
        product.Name.Should().Be("Test Product");
        product.Description.Should().Be("Test Description");
        product.Category.Should().Be("Electronics");
        product.Price.Should().Be(99.99m);
        product.StockQuantity.Should().Be(100);
        product.ReorderLevel.Should().Be(20);
    }

    [Fact]
    public void Product_IsLowStock_ShouldReturnTrue_WhenBelowReorderLevel()
    {
        // Arrange
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Test Product",
            StockQuantity = 15,
            ReorderLevel = 20
        };

        // Act & Assert
        (product.StockQuantity <= product.ReorderLevel).Should().BeTrue();
    }

    [Fact]
    public void Product_IsLowStock_ShouldReturnFalse_WhenAboveReorderLevel()
    {
        // Arrange
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Test Product",
            StockQuantity = 25,
            ReorderLevel = 20
        };

        // Act & Assert
        (product.StockQuantity > product.ReorderLevel).Should().BeTrue();
    }

    [Fact]
    public void Product_Value_ShouldBeCalculatedCorrectly()
    {
        // Arrange
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Test Product",
            Price = 50.00m,
            StockQuantity = 10
        };

        // Act
        var totalValue = product.Price * product.StockQuantity;

        // Assert
        totalValue.Should().Be(500.00m);
    }

    [Theory]
    [InlineData(0, 20, true)]
    [InlineData(10, 20, true)]
    [InlineData(20, 20, true)]
    [InlineData(21, 20, false)]
    [InlineData(100, 20, false)]
    public void Product_StockLevel_ShouldBeComparedCorrectly(int stock, int reorder, bool expectedLowStock)
    {
        // Arrange
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Test Product",
            StockQuantity = stock,
            ReorderLevel = reorder
        };

        // Act
        var isLowStock = product.StockQuantity <= product.ReorderLevel;

        // Assert
        isLowStock.Should().Be(expectedLowStock);
    }

    [Fact]
    public void Product_WithExpirationTracking_ShouldHaveExpirationDate()
    {
        // Arrange
        var product = new Product
        {
            SKU = "PROD-001",
            Name = "Perishable Product",
            TrackExpiration = true,
            ExpirationDate = DateTime.UtcNow.AddDays(30)
        };

        // Assert
        product.TrackExpiration.Should().BeTrue();
        product.ExpirationDate.Should().NotBeNull();
        product.ExpirationDate.Should().BeAfter(DateTime.UtcNow);
    }
}
