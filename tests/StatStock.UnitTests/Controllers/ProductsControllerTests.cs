using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StatStock.Domain.Entities;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.Controllers;
using StatStock.Web.Api.DTOs;

namespace StatStock.UnitTests.Controllers;

public class ProductsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ProductsController>> _loggerMock;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ProductsController>>();
        _controller = new ProductsController(_context, _loggerMock.Object);
    }

    #region GetProducts Tests

    [Fact]
    public async Task GetProducts_ShouldReturnAllProducts_WhenNoFiltersApplied()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByCategory()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(category: "Electronics");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().HaveCount(2);
        response.Data.Should().OnlyContain(p => p.Category == "Electronics");
    }

    [Fact]
    public async Task GetProducts_ShouldFilterBySearch()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(search: "Laptop");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().HaveCount(1);
        response.Data.First().Name.Should().Contain("Laptop");
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByMinStock()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(minStock: 50);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().OnlyContain(p => p.StockQuantity >= 50);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByMaxStock()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(maxStock: 30);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().OnlyContain(p => p.StockQuantity <= 30);
    }

    [Fact]
    public async Task GetProducts_ShouldCombineMultipleFilters()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(
            category: "Electronics",
            minStock: 40,
            maxStock: 60);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProducts_ShouldReturnEmptyList_WhenNoProductsMatch()
    {
        // Arrange
        await SeedProducts();

        // Act
        var result = await _controller.GetProducts(category: "NonExistent");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().BeEmpty();
    }

    #endregion

    #region GetProduct Tests

    [Fact]
    public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
    {
        // Arrange
        var product = await CreateProduct("PROD-001", "Test Product", 100, 20, 99.99m);

        // Act
        var result = await _controller.GetProduct(product.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Id.Should().Be(product.Id);
        response.Data.SKU.Should().Be("PROD-001");
    }

    [Fact]
    public async Task GetProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Act
        var result = await _controller.GetProduct(999);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Product not found");
    }

    #endregion

    #region CreateProduct Tests

    [Fact]
    public async Task CreateProduct_ShouldCreateProduct_WithValidData()
    {
        // Arrange
        var createDto = new CreateProductDto
        {
            SKU = "NEW-001",
            Name = "New Product",
            Description = "A new product",
            Price = 49.99m,
            Category = "Electronics",
            ReorderLevel = 10,
            StockQuantity = 50
        };

        // Act
        var result = await _controller.CreateProduct(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.SKU.Should().Be("NEW-001");
        response.Data.Name.Should().Be("New Product");
        response.Data.Price.Should().Be(49.99m);

        // Verify it's in the database
        var dbProduct = await _context.Products.FindAsync(response.Data.Id);
        dbProduct.Should().NotBeNull();
        dbProduct!.SKU.Should().Be("NEW-001");
    }

    [Fact]
    public async Task CreateProduct_ShouldReturnBadRequest_WhenSKUAlreadyExists()
    {
        // Arrange
        await CreateProduct("DUP-001", "Existing Product", 100, 20, 99.99m);

        var createDto = new CreateProductDto
        {
            SKU = "DUP-001",
            Name = "Duplicate SKU Product",
            Description = "This should fail",
            Price = 49.99m,
            Category = "Electronics",
            ReorderLevel = 10,
            StockQuantity = 50
        };

        // Act
        var result = await _controller.CreateProduct(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("SKU already exists");
    }

    [Fact]
    public async Task CreateProduct_ShouldSetTimestamps()
    {
        // Arrange
        var createDto = new CreateProductDto
        {
            SKU = "TIME-001",
            Name = "Timestamp Test",
            Description = "Testing timestamps",
            Price = 29.99m,
            Category = "Test",
            ReorderLevel = 5,
            StockQuantity = 25
        };

        // Act
        var result = await _controller.CreateProduct(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        response.Data.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region UpdateProduct Tests

    [Fact]
    public async Task UpdateProduct_ShouldUpdateProduct_WhenValidDataProvided()
    {
        // Arrange
        var product = await CreateProduct("UPD-001", "Original Product", 100, 20, 99.99m);

        var updateDto = new UpdateProductDto
        {
            Name = "Updated Product",
            Price = 149.99m,
            StockQuantity = 150
        };

        // Act
        var result = await _controller.UpdateProduct(product.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Name.Should().Be("Updated Product");
        response.Data.Price.Should().Be(149.99m);
        response.Data.StockQuantity.Should().Be(150);
        response.Data.SKU.Should().Be("UPD-001"); // Unchanged
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var updateDto = new UpdateProductDto { Name = "Test" };

        // Act
        var result = await _controller.UpdateProduct(999, updateDto);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturnBadRequest_WhenNewSKUAlreadyExists()
    {
        // Arrange
        var product1 = await CreateProduct("EXIST-001", "Product 1", 100, 20, 99.99m);
        var product2 = await CreateProduct("EXIST-002", "Product 2", 100, 20, 99.99m);

        var updateDto = new UpdateProductDto { SKU = "EXIST-001" };

        // Act
        var result = await _controller.UpdateProduct(product2.Id, updateDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("SKU already exists");
    }

    [Fact]
    public async Task UpdateProduct_ShouldUpdateTimestamp()
    {
        // Arrange
        var product = await CreateProduct("TIME-002", "Timestamp Test", 100, 20, 99.99m);
        var originalUpdatedAt = product.UpdatedAt;
        await Task.Delay(100); // Ensure time difference

        var updateDto = new UpdateProductDto { Name = "Updated Name" };

        // Act
        var result = await _controller.UpdateProduct(product.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Data.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateProduct_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange
        var product = await CreateProduct("PARTIAL-001", "Original", 100, 20, 99.99m);

        var updateDto = new UpdateProductDto { StockQuantity = 200 };

        // Act
        var result = await _controller.UpdateProduct(product.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<ProductDto>>().Subject;
        response.Data.StockQuantity.Should().Be(200);
        response.Data.Name.Should().Be("Original"); // Unchanged
        response.Data.Price.Should().Be(99.99m); // Unchanged
    }

    #endregion

    #region DeleteProduct Tests

    [Fact]
    public async Task DeleteProduct_ShouldDeleteProduct_WhenProductExists()
    {
        // Arrange
        var product = await CreateProduct("DEL-001", "To Delete", 100, 20, 99.99m);

        // Act
        var result = await _controller.DeleteProduct(product.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        response.Success.Should().BeTrue();

        // Verify it's deleted from database
        var deletedProduct = await _context.Products.FindAsync(product.Id);
        deletedProduct.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Act
        var result = await _controller.DeleteProduct(999);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
    }

    #endregion

    #region GetCategories Tests

    [Fact]
    public async Task GetCategories_ShouldReturnDistinctCategories()
    {
        // Arrange
        await CreateProduct("CAT-001", "Product 1", 100, 20, 99.99m, "Electronics");
        await CreateProduct("CAT-002", "Product 2", 100, 20, 99.99m, "Electronics");
        await CreateProduct("CAT-003", "Product 3", 100, 20, 99.99m, "Furniture");
        await CreateProduct("CAT-004", "Product 4", 100, 20, 99.99m, "Clothing");

        // Act
        var result = await _controller.GetCategories();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<string>>>().Subject;
        response.Data.Should().HaveCount(3);
        response.Data.Should().Contain(new[] { "Electronics", "Furniture", "Clothing" });
    }

    [Fact]
    public async Task GetCategories_ShouldReturnOrderedCategories()
    {
        // Arrange
        await CreateProduct("ORD-001", "Product 1", 100, 20, 99.99m, "Zebra");
        await CreateProduct("ORD-002", "Product 2", 100, 20, 99.99m, "Apple");
        await CreateProduct("ORD-003", "Product 3", 100, 20, 99.99m, "Mango");

        // Act
        var result = await _controller.GetCategories();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<string>>>().Subject;
        response.Data.Should().BeInAscendingOrder();
        response.Data[0].Should().Be("Apple");
        response.Data[2].Should().Be("Zebra");
    }

    #endregion

    #region GetLowStockProducts Tests

    [Fact]
    public async Task GetLowStockProducts_ShouldReturnOnlyLowStockProducts()
    {
        // Arrange
        await CreateProduct("LOW-001", "Low Stock 1", 5, 20, 99.99m);
        await CreateProduct("LOW-002", "Low Stock 2", 15, 20, 99.99m);
        await CreateProduct("GOOD-001", "Good Stock", 50, 20, 99.99m);

        // Act
        var result = await _controller.GetLowStockProducts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().HaveCount(2);
        response.Data.Should().OnlyContain(p => p.StockQuantity <= p.ReorderLevel);
    }

    [Fact]
    public async Task GetLowStockProducts_ShouldOrderByStockQuantity()
    {
        // Arrange
        await CreateProduct("LOW-003", "Stock 15", 15, 20, 99.99m);
        await CreateProduct("LOW-004", "Stock 5", 5, 20, 99.99m);
        await CreateProduct("LOW-005", "Stock 10", 10, 20, 99.99m);

        // Act
        var result = await _controller.GetLowStockProducts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<ProductDto>>>().Subject;
        response.Data.Should().BeInAscendingOrder(p => p.StockQuantity);
    }

    #endregion

    #region Helper Methods

    private async Task SeedProducts()
    {
        var products = new[]
        {
            new Product
            {
                SKU = "LAPTOP-001",
                Name = "Gaming Laptop",
                Description = "High performance laptop",
                Price = 1299.99m,
                Category = "Electronics",
                StockQuantity = 25,
                ReorderLevel = 10
            },
            new Product
            {
                SKU = "MOUSE-001",
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse",
                Price = 29.99m,
                Category = "Electronics",
                StockQuantity = 50,
                ReorderLevel = 20
            },
            new Product
            {
                SKU = "DESK-001",
                Name = "Office Desk",
                Description = "Adjustable standing desk",
                Price = 499.99m,
                Category = "Furniture",
                StockQuantity = 15,
                ReorderLevel = 5
            }
        };

        _context.Products.AddRange(products);
        await _context.SaveChangesAsync();
    }

    private async Task<Product> CreateProduct(string sku, string name, int stockQuantity, int reorderLevel, decimal price, string category = "Electronics")
    {
        var product = new Product
        {
            SKU = sku,
            Name = name,
            Description = $"Description for {name}",
            Price = price,
            Category = category,
            StockQuantity = stockQuantity,
            ReorderLevel = reorderLevel,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
