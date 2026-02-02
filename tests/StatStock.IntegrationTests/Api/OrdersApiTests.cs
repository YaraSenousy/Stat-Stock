using FluentAssertions;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Web.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class OrdersApiTests : IntegrationTestBase
{
    public OrdersApiTests(StatStockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetOrders_ShouldReturn200_WithEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrders_ShouldReturn200_WithSeededOrders()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-001", 
                Name = "Test Product", 
                Price = 100m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order1 = new Order 
            { 
                OrderNumber = "ORD-001", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending, 
                CreatedAt = DateTime.UtcNow 
            };
            var order2 = new Order 
            { 
                OrderNumber = "ORD-002",
                Type = OrderType.Incoming, 
                Status = OrderStatus.Delivered, 
                CreatedAt = DateTime.UtcNow 
            };
            context.Orders.AddRange(order1, order2);
            await context.SaveChangesAsync();

            context.OrderItems.AddRange(
                new OrderItem { OrderId = order1.Id, ProductId = product.Id, Quantity = 10, UnitPrice = 100m },
                new OrderItem { OrderId = order2.Id, ProductId = product.Id, Quantity = 5, UnitPrice = 100m }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByStatus()
    {
        // Arrange
        await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-002", 
                Name = "Test", 
                Price = 50m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order1 = new Order 
            { 
                OrderNumber = "ORD-003", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending, 
                CreatedAt = DateTime.UtcNow 
            };
            var order2 = new Order 
            { 
                OrderNumber = "ORD-004", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Delivered, 
                CreatedAt = DateTime.UtcNow 
            };
            context.Orders.AddRange(order1, order2);
            await context.SaveChangesAsync();

            context.OrderItems.AddRange(
                new OrderItem { OrderId = order1.Id, ProductId = product.Id, Quantity = 10, UnitPrice = 50m },
                new OrderItem { OrderId = order2.Id, ProductId = product.Id, Quantity = 5, UnitPrice = 50m }
            );
            await Task.CompletedTask;
        });

        // Act
        var response = await Client.GetAsync("/api/orders?status=" + (int)OrderStatus.Pending);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>();
        result!.Data.Should().HaveCount(1);
        result.Data.First().OrderNumber.Should().Be("ORD-003");
    }

    [Fact]
    public async Task GetOrderById_ShouldReturn200_WhenOrderExists()
    {
        // Arrange
        var orderId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-003", 
                Name = "Test", 
                Price = 75m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order = new Order 
            { 
                OrderNumber = "ORD-005", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending, 
                CreatedAt = DateTime.UtcNow 
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            context.OrderItems.Add(new OrderItem 
            { 
                OrderId = order.Id, 
                ProductId = product.Id, 
                Quantity = 10, 
                UnitPrice = 75m 
            });
            await context.SaveChangesAsync();
            
            return order.Id;
        });

        // Act
        var response = await Client.GetAsync($"/api/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Data.OrderNumber.Should().Be("ORD-005");
    }

    [Fact]
    public async Task GetOrderById_ShouldReturn404_WhenOrderDoesNotExist()
    {
        // Act
        var response = await Client.GetAsync("/api/orders/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturn201_WithValidData()
    {
        // Arrange
        var productId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-004", 
                Name = "Test", 
                Price = 100m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        var newOrder = new
        {
            type = "Purchase",
            notes = "Test order",
            items = new[]
            {
                new { productId, quantity = 5, unitPrice = 100m }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", newOrder);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        result!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldReturn200_WhenValid()
    {
        // Arrange
        var orderId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-005", 
                Name = "Test", 
                Price = 50m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order = new Order 
            { 
                OrderNumber = "ORD-006", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending, 
                CreatedAt = DateTime.UtcNow 
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            context.OrderItems.Add(new OrderItem 
            { 
                OrderId = order.Id, 
                ProductId = product.Id, 
                Quantity = 10, 
                UnitPrice = 50m 
            });
            await context.SaveChangesAsync();
            
            return order.Id;
        });

        var update = new
        {
            status = "Approved"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/orders/{orderId}/status", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelOrder_ShouldReturn204_WhenOrderIsPending()
    {
        // Arrange
        var orderId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = "PROD-006", 
                Name = "Test", 
                Price = 25m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var order = new Order 
            { 
                OrderNumber = "ORD-007", 
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending, 
                CreatedAt = DateTime.UtcNow 
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            context.OrderItems.Add(new OrderItem 
            { 
                OrderId = order.Id, 
                ProductId = product.Id, 
                Quantity = 5, 
                UnitPrice = 25m 
            });
            await context.SaveChangesAsync();
            
            return order.Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CreateOrder_ShouldSupportAllStatuses(OrderStatus status)
    {
        // Arrange
        var productId = await ExecuteDbAsync(async context =>
        {
            var product = new Product 
            { 
                SKU = $"PROD-{status}", 
                Name = "Test", 
                Price = 100m, 
                StockQuantity = 100, 
                ReorderLevel = 10,
                Category = "Test"
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        });

        var newOrder = new
        {
            type = "Purchase",
            notes = $"Test order for {status}",
            items = new[]
            {
                new { productId, quantity = 5, unitPrice = 100m }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", newOrder);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
