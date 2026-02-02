using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StatStock.Domain.Entities;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class ProductsApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private ApplicationDbContext _context = null!;
    private string _authToken = string.Empty;

    public ProductsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all DbContext registrations
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext)).ToList();
                
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.UseInMemoryDatabase("ProductsApiTestDb_" + Guid.NewGuid());
                });
            });
        });
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        
        using var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        _authToken = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    #region GET /api/products

    [Fact]
    public async Task GetProducts_ShouldReturn200_WithListOfProducts()
    {
        // Arrange
        await SeedProducts();

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetProducts_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByCategory()
    {
        // Arrange
        await SeedProducts();

        // Act
        var response = await _client.GetAsync("/api/products?category=Electronics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().OnlyContain(p => p.Category == "Electronics");
    }

    [Fact]
    public async Task GetProducts_ShouldFilterBySearch()
    {
        // Arrange
        await SeedProducts();

        // Act
        var response = await _client.GetAsync("/api/products?search=Laptop");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().HaveCountGreaterThan(0);
        result.Data.Should().OnlyContain(p => p.Name.Contains("Laptop") || p.SKU.Contains("Laptop"));
    }

    [Fact]
    public async Task GetProducts_ShouldFilterByStockRange()
    {
        // Arrange
        await SeedProducts();

        // Act
        var response = await _client.GetAsync("/api/products?minStock=30&maxStock=60");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Data.Should().OnlyContain(p => p.StockQuantity >= 30 && p.StockQuantity <= 60);
    }

    #endregion

    #region GET /api/products/{id}

    [Fact]
    public async Task GetProduct_ShouldReturn200_WhenProductExists()
    {
        // Arrange
        var product = await CreateProduct("GET-001", "Get Product Test", 100, 20, 99.99m);

        // Act
        var response = await _client.GetAsync($"/api/products/{product.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Id.Should().Be(product.Id);
        result.Data.SKU.Should().Be("GET-001");
    }

    [Fact]
    public async Task GetProduct_ShouldReturn404_WhenProductDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/products/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Success.Should().BeFalse();
    }

    #endregion

    #region POST /api/products

    [Fact]
    public async Task CreateProduct_ShouldReturn201_WithValidData()
    {
        // Arrange
        var createDto = new CreateProductDto
        {
            SKU = "CREATE-001",
            Name = "New Product",
            Description = "A brand new product",
            Price = 149.99m,
            Category = "Electronics",
            ReorderLevel = 15,
            StockQuantity = 75
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.SKU.Should().Be("CREATE-001");
        result.Data.Name.Should().Be("New Product");
        result.Data.Price.Should().Be(149.99m);

        // Verify in database
        var dbProduct = await _context.Products.FirstOrDefaultAsync(p => p.SKU == "CREATE-001");
        dbProduct.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProduct_ShouldReturn400_WhenSKUAlreadyExists()
    {
        // Arrange
        await CreateProduct("DUP-001", "Existing", 100, 20, 99.99m);

        var createDto = new CreateProductDto
        {
            SKU = "DUP-001",
            Name = "Duplicate SKU",
            Description = "This should fail",
            Price = 99.99m,
            Category = "Electronics",
            ReorderLevel = 10,
            StockQuantity = 50
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("SKU already exists");
    }

    [Fact]
    public async Task CreateProduct_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var createDto = new CreateProductDto
        {
            SKU = "UNAUTH-001",
            Name = "Unauthorized",
            Price = 99.99m,
            Category = "Test",
            ReorderLevel = 10,
            StockQuantity = 50
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PUT /api/products/{id}

    [Fact]
    public async Task UpdateProduct_ShouldReturn200_WithValidData()
    {
        // Arrange
        var product = await CreateProduct("UPDATE-001", "Original Name", 100, 20, 99.99m);

        var updateDto = new UpdateProductDto
        {
            Name = "Updated Name",
            Price = 149.99m,
            StockQuantity = 150
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Name.Should().Be("Updated Name");
        result.Data.Price.Should().Be(149.99m);
        result.Data.StockQuantity.Should().Be(150);

        // Verify in database
        var dbProduct = await _context.Products.FindAsync(product.Id);
        dbProduct!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturn404_WhenProductDoesNotExist()
    {
        // Arrange
        var updateDto = new UpdateProductDto { Name = "Test" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/products/99999", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateProduct_ShouldReturn400_WhenNewSKUAlreadyExists()
    {
        // Arrange
        var product1 = await CreateProduct("EXIST-001", "Product 1", 100, 20, 99.99m);
        var product2 = await CreateProduct("EXIST-002", "Product 2", 100, 20, 99.99m);

        var updateDto = new UpdateProductDto { SKU = "EXIST-001" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/products/{product2.Id}", updateDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        result!.Message.Should().Contain("SKU already exists");
    }

    #endregion

    #region DELETE /api/products/{id}

    [Fact]
    public async Task DeleteProduct_ShouldReturn200_WhenProductExists()
    {
        // Arrange
        var product = await CreateProduct("DELETE-001", "To Delete", 100, 20, 99.99m);

        // Act
        var response = await _client.DeleteAsync($"/api/products/{product.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        result!.Success.Should().BeTrue();

        // Verify deleted from database
        var dbProduct = await _context.Products.FindAsync(product.Id);
        dbProduct.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProduct_ShouldReturn404_WhenProductDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync("/api/products/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/products/categories

    [Fact]
    public async Task GetCategories_ShouldReturn200_WithDistinctCategories()
    {
        // Arrange
        await CreateProduct("CAT-001", "Product 1", 100, 20, 99.99m, "Electronics");
        await CreateProduct("CAT-002", "Product 2", 100, 20, 99.99m, "Electronics");
        await CreateProduct("CAT-003", "Product 3", 100, 20, 99.99m, "Furniture");

        // Act
        var response = await _client.GetAsync("/api/products/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
        result!.Success.Should().BeTrue();
        result.Data.Should().Contain("Electronics");
        result.Data.Should().Contain("Furniture");
        result.Data.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region GET /api/products/low-stock

    [Fact]
    public async Task GetLowStockProducts_ShouldReturn200_WithOnlyLowStockProducts()
    {
        // Arrange
        await CreateProduct("LOW-001", "Low Stock 1", 5, 20, 99.99m);
        await CreateProduct("LOW-002", "Low Stock 2", 15, 20, 99.99m);
        await CreateProduct("GOOD-001", "Good Stock", 100, 20, 99.99m);

        // Act
        var response = await _client.GetAsync("/api/products/low-stock");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductDto>>>();
        result!.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.Should().OnlyContain(p => p.StockQuantity <= p.ReorderLevel);
    }

    #endregion

    #region Helper Methods

    private async Task<string> GetAuthTokenAsync()
    {
        var tokenRequest = new
        {
            Email = "test@example.com",
            ApiKey = "demo-api-key-12345"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/token", tokenRequest);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse?.Token ?? string.Empty;
    }

    private async Task SeedProducts()
    {
        var products = new[]
        {
            new Product
            {
                SKU = "LAPTOP-001",
                Name = "Gaming Laptop",
                Description = "High performance",
                Price = 1299.99m,
                Category = "Electronics",
                StockQuantity = 25,
                ReorderLevel = 10
            },
            new Product
            {
                SKU = "MOUSE-001",
                Name = "Wireless Mouse",
                Description = "Ergonomic",
                Price = 29.99m,
                Category = "Electronics",
                StockQuantity = 50,
                ReorderLevel = 20
            },
            new Product
            {
                SKU = "DESK-001",
                Name = "Office Desk",
                Description = "Adjustable",
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
            ReorderLevel = reorderLevel
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    #endregion
}
