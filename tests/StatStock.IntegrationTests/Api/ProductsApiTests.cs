using FluentAssertions;
using StatStock.Domain.Entities;
using StatStock.Web.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class ProductsApiTests : IntegrationTestBase
{
    public ProductsApiTests(StatStockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetProducts_ShouldReturn200_WithEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/products");

        // Log response for debugging
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Status: {response.StatusCode}, Content: {content}");
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProducts_ShouldReturn200_WithSeededProducts()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            context.Products.AddRange(
                new Product { SKU = "TEST-001", Name = "Product 1", Price = 10.00m, StockQuantity = 100, ReorderLevel = 10, Category = "Test" },
                new Product { SKU = "TEST-002", Name = "Product 2", Price = 20.00m, StockQuantity = 50, ReorderLevel = 5, Category = "Test" }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByCategory()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            context.Products.AddRange(
                new Product { SKU = "ELEC-001", Name = "Laptop", Price = 1000m, StockQuantity = 10, ReorderLevel = 2, Category = "Electronics" },
                new Product { SKU = "FURN-001", Name = "Chair", Price = 200m, StockQuantity = 20, ReorderLevel = 5, Category = "Furniture" }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/products?category=Electronics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().HaveCount(1);
        result.Data.First().Category.Should().Be("Electronics");
    }

    [Fact]
    public async Task GetProductById_ShouldReturn200_WhenProductExists()
    {
        // Arrange
        var productId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "TEST-001", 
                Name = "Test Product", 
                Price = 50m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test" 
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        // Act
        var response = await Client.GetAsync($"/api/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Data.SKU.Should().Be("TEST-001");
    }

    [Fact]
    public async Task GetProductById_ShouldReturn404_WhenProductDoesNotExist()
    {
        // Act
        var response = await Client.GetAsync("/api/products/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProduct_ShouldReturn201_WithValidData()
    {
        // Arrange
        var newProduct = new
        {
            sku = "NEW-001",
            name = "New Product",
            description = "Test product",
            price = 99.99m,
            category = "Test",
            stockQuantity = 50,
            reorderLevel = 10
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Data.SKU.Should().Be("NEW-001");
    }

    [Fact]
    public async Task CreateProduct_ShouldReturn400_WhenSKUDuplicate()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            context.Products.Add(new Product 
            { 
                SKU = "DUP-001", 
                Name = "Existing", 
                Price = 10m, 
                StockQuantity = 10, 
                ReorderLevel = 5,
                Category = "Test" 
            });
            await Task.CompletedTask;
        });

        var newProduct = new
        {
            sku = "DUP-001",
            name = "Duplicate",
            price = 20m,
            category = "Test",
            stockQuantity = 10,
            reorderLevel = 5
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturn200_WhenValid()
    {
        // Arrange
        var productId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "UPD-001", 
                Name = "Original", 
                Price = 100m, 
                StockQuantity = 10, 
                ReorderLevel = 5,
                Category = "Test" 
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        var update = new
        {
            name = "Updated Name",
            price = 150m
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/products/{productId}", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteProduct_ShouldReturn204_WhenProductExists()
    {
        // Arrange
        var productId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "DEL-001", 
                Name = "To Delete", 
                Price = 10m, 
                StockQuantity = 10, 
                ReorderLevel = 5,
                Category = "Test" 
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCategories_ShouldReturnDistinctCategories()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            context.Products.AddRange(
                new Product { SKU = "CAT-001", Name = "P1", Price = 10m, StockQuantity = 10, ReorderLevel = 5, Category = "Electronics" },
                new Product { SKU = "CAT-002", Name = "P2", Price = 20m, StockQuantity = 10, ReorderLevel = 5, Category = "Electronics" },
                new Product { SKU = "CAT-003", Name = "P3", Price = 30m, StockQuantity = 10, ReorderLevel = 5, Category = "Furniture" }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/products/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
        result!.Data.Should().HaveCount(2);
        result.Data.Should().Contain(new[] { "Electronics", "Furniture" });
    }

    [Fact]
    public async Task GetLowStockProducts_ShouldReturnOnlyLowStock()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            context.Products.AddRange(
                new Product { SKU = "LOW-001", Name = "Low Stock", Price = 10m, StockQuantity = 5, ReorderLevel = 10, Category = "Test" },
                new Product { SKU = "OK-001", Name = "OK Stock", Price = 20m, StockQuantity = 50, ReorderLevel = 10, Category = "Test" }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/products/low-stock");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().HaveCount(1);
        result.Data.First().SKU.Should().Be("LOW-001");
    }
}
