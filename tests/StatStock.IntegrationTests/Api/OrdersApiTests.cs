using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private ApplicationDbContext _context = null!;
    private string _authToken = string.Empty;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("OrdersApiTestDb_" + Guid.NewGuid());
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

    #region GET /api/orders

    [Fact]
    public async Task GetOrders_ShouldReturn200_WithListOfOrders()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetOrders_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByStatus()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync($"/api/orders?status={OrderStatus.Pending}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Data.Should().OnlyContain(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByType()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync($"/api/orders?type={OrderType.Incoming}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Data.Should().OnlyContain(o => o.Type == OrderType.Incoming);
    }

    [Fact]
    public async Task GetOrders_ShouldIncludeOrderItemsAndTotalAmount()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Data.Should().AllSatisfy(order =>
        {
            order.Items.Should().NotBeEmpty();
            order.TotalAmount.Should().BeGreaterThan(0);
            order.TotalAmount.Should().Be(order.Items.Sum(i => i.Subtotal));
        });
    }

    #endregion

    #region GET /api/orders/{id}

    [Fact]
    public async Task GetOrder_ShouldReturn200_WhenOrderExists()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);

        // Act
        var response = await _client.GetAsync($"/api/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Id.Should().Be(order.Id);
        result.Data.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrder_ShouldReturn404_WhenOrderDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/orders

    [Fact]
    public async Task CreateOrder_ShouldReturn201_WithValidIncomingOrder()
    {
        // Arrange
        var product = await CreateProduct("ORD-PROD-001", 100);
        var supplier = await CreateSupplier();

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Notes = "Test incoming order",
            SupplierId = supplier.Id,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 50, UnitPrice = 45.50m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Type.Should().Be(OrderType.Incoming);
        result.Data.Status.Should().Be(OrderStatus.Pending);
        result.Data.TotalAmount.Should().Be(2275m); // 50 * 45.50
        result.Data.OrderNumber.Should().StartWith("ORD-");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn201_WithValidOutgoingOrder()
    {
        // Arrange
        var product = await CreateProduct("ORD-PROD-002", 100);

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Outgoing,
            Notes = "Test outgoing order",
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 20, UnitPrice = 50m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Type.Should().Be(OrderType.Outgoing);
        result.Data.TotalAmount.Should().Be(1000m); // 20 * 50
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn400_WhenNoItems()
    {
        // Arrange
        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Message.Should().Contain("at least one item");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn400_WhenInsufficientStock()
    {
        // Arrange
        var product = await CreateProduct("ORD-PROD-003", 10); // Only 10 in stock

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Outgoing,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 50, UnitPrice = 50m } // Requesting 50
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Message.Should().Contain("Stock validation failed");
        result.Errors.Should().Contain(e => e.Contains("Insufficient stock"));
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn400_WhenProductNotFound()
    {
        // Arrange
        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 99999, Quantity = 10, UnitPrice = 50m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Message.Should().Contain("Products not found");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn400_WhenQuantityIsZeroOrNegative()
    {
        // Arrange
        var product = await CreateProduct("ORD-PROD-004", 100);

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 0, UnitPrice = 50m }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", createDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Message.Should().Contain("greater than zero");
    }

    #endregion

    #region PATCH /api/orders/{id}/status

    [Fact]
    public async Task UpdateOrderStatus_ShouldReturn200_WhenStatusUpdated()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);
        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Approved };

        // Act
        var response = await _client.PatchAsync(
            $"/api/orders/{order.Id}/status",
            JsonContent.Create(updateDto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Status.Should().Be(OrderStatus.Approved);
        result.Data.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldReturn404_WhenOrderNotFound()
    {
        // Arrange
        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Approved };

        // Act
        var response = await _client.PatchAsync(
            "/api/orders/99999/status",
            JsonContent.Create(updateDto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateOrderStatus_ShouldSupportAllStatuses(OrderStatus newStatus)
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);
        var updateDto = new UpdateOrderStatusDto { Status = newStatus };

        // Act
        var response = await _client.PatchAsync(
            $"/api/orders/{order.Id}/status",
            JsonContent.Create(updateDto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Data.Status.Should().Be(newStatus);
    }

    #endregion

    #region POST /api/orders/{id}/cancel

    [Fact]
    public async Task CancelOrder_ShouldReturn200_WhenOrderCancelled()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);

        // Act
        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Success.Should().BeTrue();
        result.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ShouldReturn400_WhenOrderIsDelivered()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Delivered);

        // Act
        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Message.Should().Contain("Cannot cancel");
    }

    [Fact]
    public async Task CancelOrder_ShouldReturn400_WhenOrderAlreadyCancelled()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Cancelled);

        // Act
        var response = await _client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelOrder_ShouldReturn404_WhenOrderNotFound()
    {
        // Act
        var response = await _client.PostAsync("/api/orders/99999/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/orders/my-orders

    [Fact]
    public async Task GetMyOrders_ShouldReturn200_WithUserOrders()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync("/api/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMyOrders_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        await SeedOrders();

        // Act
        var response = await _client.GetAsync("/api/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        
        // Verify descending order
        for (int i = 0; i < result!.Data.Count - 1; i++)
        {
            result.Data[i].CreatedAt.Should().BeOnOrAfter(result.Data[i + 1].CreatedAt);
        }
    }

    #endregion

    #region Helper Methods

    private async Task<string> GetAuthTokenAsync()
    {
        var tokenRequest = new { Email = "test@example.com", ApiKey = "demo-api-key-12345" };
        var response = await _client.PostAsJsonAsync("/api/auth/token", tokenRequest);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse?.Token ?? string.Empty;
    }

    private async Task SeedOrders()
    {
        var product = await CreateProduct("SEED-PROD-001", 100);
        var supplier = await CreateSupplier();

        var orders = new[]
        {
            new Order
            {
                OrderNumber = "ORD-SEED-001",
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UserId = "test-user",
                SupplierId = supplier.Id,
                Items = new List<OrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
                }
            },
            new Order
            {
                OrderNumber = "ORD-SEED-002",
                Type = OrderType.Outgoing,
                Status = OrderStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UserId = "test-user",
                Items = new List<OrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 5, UnitPrice = 50m }
                }
            }
        };

        _context.Orders.AddRange(orders);
        await _context.SaveChangesAsync();
    }

    private async Task<Order> CreateOrder(OrderType type, OrderStatus status)
    {
        var product = await CreateProduct($"PROD-{Guid.NewGuid().ToString()[..8]}", 100);
        var supplier = await CreateSupplier();

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..8]}",
            Type = type,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UserId = "test-user",
            SupplierId = supplier.Id,
            Items = new List<OrderItem>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
            }
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    private async Task<Product> CreateProduct(string sku, int stockQuantity)
    {
        var product = new Product
        {
            SKU = sku,
            Name = $"Product {sku}",
            Description = "Test product",
            Price = 50m,
            Category = "Electronics",
            StockQuantity = stockQuantity,
            ReorderLevel = 20
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    private async Task<Supplier> CreateSupplier()
    {
        var supplier = new Supplier
        {
            Name = "Test Supplier",
            Contact = "John Doe",
            Email = "supplier@test.com",
            Phone = "+1-555-0123"
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    #endregion
}
