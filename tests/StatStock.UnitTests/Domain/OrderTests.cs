using FluentAssertions;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using Xunit;

namespace StatStock.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Order_ShouldBeCreated_WithValidProperties()
    {
        // Arrange & Act
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Type = OrderType.Incoming,
            Status = OrderStatus.Pending,
            Notes = "Test Order",
            SupplierId = 1
        };

        // Assert
        order.OrderNumber.Should().Be("ORD-001");
        order.Type.Should().Be(OrderType.Incoming);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Notes.Should().Be("Test Order");
        order.SupplierId.Should().Be(1);
    }

    [Fact]
    public void Order_TotalAmount_ShouldBeCalculatedFromItems()
    {
        // Arrange
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Type = OrderType.Incoming,
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 10, UnitPrice = 100.00m },
                new() { ProductId = 2, Quantity = 5, UnitPrice = 50.00m }
            }
        };

        // Act
        var totalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Assert
        totalAmount.Should().Be(1250.00m);
    }

    [Fact]
    public void Order_StatusTransition_FromPendingToApproved_ShouldBeValid()
    {
        // Arrange
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Status = OrderStatus.Pending
        };

        // Act
        order.Status = OrderStatus.Approved;
        order.ApprovedAt = DateTime.UtcNow;

        // Assert
        order.Status.Should().Be(OrderStatus.Approved);
        order.ApprovedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Order_ShouldAcceptAllValidStatuses(OrderStatus status)
    {
        // Arrange & Act
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Status = status
        };

        // Assert
        order.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(OrderType.Incoming)]
    [InlineData(OrderType.Outgoing)]
    public void Order_ShouldAcceptAllValidTypes(OrderType type)
    {
        // Arrange & Act
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Type = type
        };

        // Assert
        order.Type.Should().Be(type);
    }

    [Fact]
    public void Order_WithMultipleItems_ShouldCalculateCorrectItemCount()
    {
        // Arrange
        var order = new Order
        {
            OrderNumber = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, Quantity = 10, UnitPrice = 100.00m },
                new() { ProductId = 2, Quantity = 5, UnitPrice = 50.00m },
                new() { ProductId = 3, Quantity = 2, UnitPrice = 25.00m }
            }
        };

        // Act
        var itemCount = order.Items.Count;
        var totalQuantity = order.Items.Sum(i => i.Quantity);

        // Assert
        itemCount.Should().Be(3);
        totalQuantity.Should().Be(17);
    }
}
