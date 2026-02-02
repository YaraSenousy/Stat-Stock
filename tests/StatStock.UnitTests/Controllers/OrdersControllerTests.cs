using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.Controllers;
using StatStock.Web.Api.DTOs;
using StatStock.Web.Api.Services;
using System.Security.Claims;

namespace StatStock.UnitTests.Controllers;

public class OrdersControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<OrdersController>> _loggerMock;
    private readonly Mock<IWebhookService> _webhookServiceMock;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<OrdersController>>();
        _webhookServiceMock = new Mock<IWebhookService>();
        _controller = new OrdersController(_context, _loggerMock.Object, _webhookServiceMock.Object);

        // Setup user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-123"),
            new Claim(ClaimTypes.Email, "test@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetOrders Tests

    [Fact]
    public async Task GetOrders_ShouldReturnAllOrders_WhenNoFiltersApplied()
    {
        // Arrange
        await SeedOrders();

        // Act
        var result = await _controller.GetOrders();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByStatus()
    {
        // Arrange
        await SeedOrders();

        // Act
        var result = await _controller.GetOrders(status: OrderStatus.Pending);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().OnlyContain(o => o.Status == OrderStatus.Pending);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByType()
    {
        // Arrange
        await SeedOrders();

        // Act
        var result = await _controller.GetOrders(type: OrderType.Incoming);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().OnlyContain(o => o.Type == OrderType.Incoming);
    }

    [Fact]
    public async Task GetOrders_ShouldFilterByDateRange()
    {
        // Arrange
        await SeedOrders();
        var fromDate = DateTime.UtcNow.AddDays(-5);
        var toDate = DateTime.UtcNow.AddDays(5);

        // Act
        var result = await _controller.GetOrders(fromDate: fromDate, toDate: toDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().OnlyContain(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate.Date.AddDays(1).AddTicks(-1));
    }

    [Fact]
    public async Task GetOrders_ShouldIncludeOrderItems()
    {
        // Arrange
        await SeedOrders();

        // Act
        var result = await _controller.GetOrders();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().AllSatisfy(order =>
        {
            order.Items.Should().NotBeEmpty();
            order.TotalAmount.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region GetOrder Tests

    [Fact]
    public async Task GetOrder_ShouldReturnOrder_WhenOrderExists()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);

        // Act
        var result = await _controller.GetOrder(order.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Id.Should().Be(order.Id);
        response.Data.OrderNumber.Should().Be(order.OrderNumber);
    }

    [Fact]
    public async Task GetOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        // Act
        var result = await _controller.GetOrder(999);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
    }

    #endregion

    #region CreateOrder Tests

    [Fact]
    public async Task CreateOrder_ShouldCreateOrder_WithValidData()
    {
        // Arrange
        var product = await CreateProduct("PROD-001", 100);
        var supplier = await CreateSupplier();

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Notes = "Test order",
            SupplierId = supplier.Id,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Type.Should().Be(OrderType.Incoming);
        response.Data.Status.Should().Be(OrderStatus.Pending);
        response.Data.Items.Should().HaveCount(1);
        response.Data.TotalAmount.Should().Be(500m);

        // Verify webhook was called
        _webhookServiceMock.Verify(x => x.NotifyOrderCreated(It.IsAny<OrderDto>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenNoItems()
    {
        // Arrange
        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>()
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("at least one item");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenQuantityIsZeroOrNegative()
    {
        // Arrange
        var product = await CreateProduct("PROD-002", 100);

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 0, UnitPrice = 50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Message.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenUnitPriceIsNegative()
    {
        // Arrange
        var product = await CreateProduct("PROD-003", 100);

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = -50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Message.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenProductNotFound()
    {
        // Arrange
        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = 999, Quantity = 10, UnitPrice = 50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Message.Should().Contain("Products not found");
    }

    [Fact]
    public async Task CreateOrder_ShouldReturnBadRequest_WhenInsufficientStockForOutgoing()
    {
        // Arrange
        var product = await CreateProduct("PROD-004", 10); // Only 10 in stock

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Outgoing, // Outgoing order
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 20, UnitPrice = 50m } // Requesting 20
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Message.Should().Contain("Stock validation failed");
        response.Errors.Should().Contain(e => e.Contains("Insufficient stock"));
    }

    [Fact]
    public async Task CreateOrder_ShouldNotValidateStock_ForIncomingOrders()
    {
        // Arrange
        var product = await CreateProduct("PROD-005", 0); // Zero stock

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming, // Incoming order - no stock validation
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 50, UnitPrice = 50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateOrder_ShouldGenerateOrderNumber()
    {
        // Arrange
        var product = await CreateProduct("PROD-006", 100);

        var createDto = new CreateOrderDto
        {
            Type = OrderType.Incoming,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
            }
        };

        // Act
        var result = await _controller.CreateOrder(createDto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Data.OrderNumber.Should().StartWith("ORD-");
        response.Data.OrderNumber.Length.Should().BeGreaterThan(4);
    }

    #endregion

    #region UpdateOrderStatus Tests

    [Fact]
    public async Task UpdateOrderStatus_ShouldUpdateStatus_WhenValidStatusProvided()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);
        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Approved };

        // Act
        var result = await _controller.UpdateOrderStatus(order.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Status.Should().Be(OrderStatus.Approved);

        // Verify webhook was called
        _webhookServiceMock.Verify(x => 
            x.NotifyOrderStatusChanged(It.IsAny<OrderDto>(), OrderStatus.Pending, OrderStatus.Approved), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldSetApprovedAt_WhenStatusIsApproved()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);
        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Approved };

        // Act
        var result = await _controller.UpdateOrderStatus(order.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Data.ApprovedAt.Should().NotBeNull();
        response.Data.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task UpdateOrderStatus_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        // Arrange
        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Approved };

        // Act
        var result = await _controller.UpdateOrderStatus(999, updateDto);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateOrderStatus_ShouldSupportAllStatusTransitions(OrderStatus newStatus)
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);
        var updateDto = new UpdateOrderStatusDto { Status = newStatus };

        // Act
        var result = await _controller.UpdateOrderStatus(order.Id, updateDto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Data.Status.Should().Be(newStatus);
    }

    #endregion

    #region CancelOrder Tests

    [Fact]
    public async Task CancelOrder_ShouldCancelOrder_WhenOrderIsPending()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Pending);

        // Act
        var result = await _controller.CancelOrder(order.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Status.Should().Be(OrderStatus.Cancelled);

        // Verify webhook was called
        _webhookServiceMock.Verify(x => 
            x.NotifyOrderStatusChanged(It.IsAny<OrderDto>(), OrderStatus.Pending, OrderStatus.Cancelled), 
            Times.Once);
    }

    [Fact]
    public async Task CancelOrder_ShouldReturnBadRequest_WhenOrderIsDelivered()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Delivered);

        // Act
        var result = await _controller.CancelOrder(order.Id);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Cannot cancel");
    }

    [Fact]
    public async Task CancelOrder_ShouldReturnBadRequest_WhenOrderIsAlreadyCancelled()
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, OrderStatus.Cancelled);

        // Act
        var result = await _controller.CancelOrder(order.Id);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CancelOrder_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        // Act
        var result = await _controller.CancelOrder(999);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Shipped)]
    public async Task CancelOrder_ShouldAllowCancellation_ForNonFinalStatuses(OrderStatus status)
    {
        // Arrange
        var order = await CreateOrder(OrderType.Incoming, status);

        // Act
        var result = await _controller.CancelOrder(order.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<OrderDto>>().Subject;
        response.Data.Status.Should().Be(OrderStatus.Cancelled);
    }

    #endregion

    #region GetMyOrders Tests

    [Fact]
    public async Task GetMyOrders_ShouldReturnOnlyCurrentUserOrders()
    {
        // Arrange
        var product = await CreateProduct("PROD-007", 100);
        
        // Create orders for current user
        await CreateOrderForUser("test-user-123", product);
        await CreateOrderForUser("test-user-123", product);
        
        // Create order for different user
        await CreateOrderForUser("other-user", product);

        // Act
        var result = await _controller.GetMyOrders();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().HaveCount(2);
        response.Data.Should().OnlyContain(o => o.UserId == "test-user-123");
    }

    [Fact]
    public async Task GetMyOrders_ShouldOrderByCreatedAtDescending()
    {
        // Arrange
        var product = await CreateProduct("PROD-008", 100);
        
        var order1 = await CreateOrderForUser("test-user-123", product, DateTime.UtcNow.AddDays(-2));
        var order2 = await CreateOrderForUser("test-user-123", product, DateTime.UtcNow.AddDays(-1));
        var order3 = await CreateOrderForUser("test-user-123", product, DateTime.UtcNow);

        // Act
        var result = await _controller.GetMyOrders();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ApiResponse<List<OrderDto>>>().Subject;
        response.Data.Should().HaveCount(3);
        
        // Most recent first
        for (int i = 0; i < response.Data.Count - 1; i++)
        {
            response.Data[i].CreatedAt.Should().BeOnOrAfter(response.Data[i + 1].CreatedAt);
        }
    }

    #endregion

    #region Helper Methods

    private async Task SeedOrders()
    {
        var supplier = await CreateSupplier();
        var product = await CreateProduct("SEED-001", 100);

        var orders = new[]
        {
            new Order
            {
                OrderNumber = "ORD-001",
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UserId = "user-1",
                SupplierId = supplier.Id,
                Items = new List<OrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
                }
            },
            new Order
            {
                OrderNumber = "ORD-002",
                Type = OrderType.Outgoing,
                Status = OrderStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UserId = "user-1",
                Items = new List<OrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 5, UnitPrice = 50m }
                }
            },
            new Order
            {
                OrderNumber = "ORD-003",
                Type = OrderType.Incoming,
                Status = OrderStatus.Delivered,
                CreatedAt = DateTime.UtcNow,
                UserId = "user-2",
                SupplierId = supplier.Id,
                Items = new List<OrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 20, UnitPrice = 50m }
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
            UserId = "test-user-123",
            SupplierId = supplier.Id,
            Items = new List<OrderItem>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 50m }
            }
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Load navigation properties
        await _context.Entry(order).Collection(o => o.Items).LoadAsync();
        await _context.Entry(order).Reference(o => o.Supplier).LoadAsync();

        return order;
    }

    private async Task<Order> CreateOrderForUser(string userId, Product product, DateTime? createdAt = null)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..8]}",
            Type = OrderType.Incoming,
            Status = OrderStatus.Pending,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UserId = userId,
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
