using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StatStock.Domain.Entities;
using StatStock.Infrastructure.Data;
using StatStock.Infrastructure.Services;

namespace StatStock.UnitTests.Services;

public class AuditServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<AuditService>> _loggerMock;
    private readonly AuditService _auditService;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<AuditService>>();
        _auditService = new AuditService(_context, _loggerMock.Object);
    }

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_ShouldCreateAuditLog_WithAllProperties()
    {
        // Arrange
        var userId = "user-123";
        var userEmail = "test@example.com";
        var action = "CREATE";
        var entityType = "Product";
        var entityId = "PROD-001";
        var oldValues = "{\"name\": \"Old Name\"}";
        var newValues = "{\"name\": \"New Name\"}";
        var ipAddress = "192.168.1.1";

        // Act
        await _auditService.LogAsync(userId, userEmail, action, entityType, entityId, oldValues, newValues, ipAddress);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.UserId.Should().Be(userId);
        log.UserEmail.Should().Be(userEmail);
        log.Action.Should().Be(action);
        log.EntityType.Should().Be(entityType);
        log.EntityId.Should().Be(entityId);
        log.OldValues.Should().Be(oldValues);
        log.NewValues.Should().Be(newValues);
        log.IpAddress.Should().Be(ipAddress);
        log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task LogAsync_ShouldCreateAuditLog_WithMinimalProperties()
    {
        // Arrange
        var userId = "user-456";
        var userEmail = "minimal@example.com";
        var action = "DELETE";
        var entityType = "Order";
        var entityId = "ORD-001";

        // Act
        await _auditService.LogAsync(userId, userEmail, action, entityType, entityId);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.UserId.Should().Be(userId);
        log.UserEmail.Should().Be(userEmail);
        log.Action.Should().Be(action);
        log.EntityType.Should().Be(entityType);
        log.EntityId.Should().Be(entityId);
        log.OldValues.Should().BeNull();
        log.NewValues.Should().BeNull();
        log.IpAddress.Should().Be(string.Empty);
    }

    [Fact]
    public async Task LogAsync_ShouldHandleNullOptionalParameters()
    {
        // Arrange
        var userId = "user-789";
        var userEmail = "null@example.com";
        var action = "UPDATE";
        var entityType = "Supplier";
        var entityId = "SUP-001";

        // Act
        await _auditService.LogAsync(userId, userEmail, action, entityType, entityId, null, null);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.OldValues.Should().BeNull();
        log.NewValues.Should().BeNull();
        log.IpAddress.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("CREATE")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("READ")]
    [InlineData("APPROVE")]
    public async Task LogAsync_ShouldSupportVariousActions(string action)
    {
        // Arrange
        var userId = "user-multi";
        var userEmail = "actions@example.com";
        var entityType = "Product";
        var entityId = "PROD-" + action;

        // Act
        await _auditService.LogAsync(userId, userEmail, action, entityType, entityId);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        var log = logs.FirstOrDefault(l => l.Action == action);
        log.Should().NotBeNull();
        log!.Action.Should().Be(action);
    }

    [Theory]
    [InlineData("Product")]
    [InlineData("Order")]
    [InlineData("Supplier")]
    [InlineData("User")]
    public async Task LogAsync_ShouldSupportVariousEntityTypes(string entityType)
    {
        // Arrange
        var userId = "user-entities";
        var userEmail = "entities@example.com";
        var action = "CREATE";
        var entityId = $"{entityType}-001";

        // Act
        await _auditService.LogAsync(userId, userEmail, action, entityType, entityId);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        var log = logs.FirstOrDefault(l => l.EntityType == entityType);
        log.Should().NotBeNull();
        log!.EntityType.Should().Be(entityType);
    }

    [Fact]
    public async Task LogAsync_ShouldNotThrowException_WhenDatabaseOperationFails()
    {
        // Arrange - Create a new disposed context to simulate failure
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var disposedContext = new ApplicationDbContext(options);
        disposedContext.Dispose();
        
        var failingAuditService = new AuditService(disposedContext, _loggerMock.Object);

        var userId = "user-fail";
        var userEmail = "fail@example.com";
        var action = "CREATE";
        var entityType = "Product";
        var entityId = "PROD-FAIL";

        // Act
        var act = async () => await failingAuditService.LogAsync(userId, userEmail, action, entityType, entityId);

        // Assert
        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    #endregion

    #region GetLogsAsync Tests

    [Fact]
    public async Task GetLogsAsync_ShouldReturnAllLogs_WhenNoFiltersApplied()
    {
        // Arrange
        await CreateTestLogs(5);

        // Act
        var result = (await _auditService.GetLogsAsync()).ToList();

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByStartDate()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.AddDays(-10);
        await _auditService.LogAsync("user-1", "test1@example.com", "CREATE", "Product", "1");
        await Task.Delay(100); // Ensure different timestamps
        
        var startDate = DateTime.UtcNow.AddMinutes(-1);
        await _auditService.LogAsync("user-2", "test2@example.com", "UPDATE", "Product", "2");
        await _auditService.LogAsync("user-3", "test3@example.com", "DELETE", "Product", "3");

        // Act
        var result = (await _auditService.GetLogsAsync(startDate: startDate)).ToList();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().OnlyContain(l => l.Timestamp >= startDate);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByEndDate()
    {
        // Arrange
        await _auditService.LogAsync("user-1", "test1@example.com", "CREATE", "Product", "1");
        await _auditService.LogAsync("user-2", "test2@example.com", "UPDATE", "Product", "2");
        
        var endDate = DateTime.UtcNow;
        await Task.Delay(100);
        await _auditService.LogAsync("user-3", "test3@example.com", "DELETE", "Product", "3");

        // Act
        var result = (await _auditService.GetLogsAsync(endDate: endDate)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.Timestamp <= endDate);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByUserId()
    {
        // Arrange
        await _auditService.LogAsync("user-target", "target@example.com", "CREATE", "Product", "1");
        await _auditService.LogAsync("user-other", "other@example.com", "UPDATE", "Product", "2");
        await _auditService.LogAsync("user-target", "target@example.com", "DELETE", "Product", "3");
        await _auditService.LogAsync("user-other", "other@example.com", "CREATE", "Order", "4");

        // Act
        var result = (await _auditService.GetLogsAsync(userId: "user-target")).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.UserId == "user-target");
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByEntityType()
    {
        // Arrange
        await _auditService.LogAsync("user-1", "test1@example.com", "CREATE", "Product", "1");
        await _auditService.LogAsync("user-2", "test2@example.com", "UPDATE", "Order", "2");
        await _auditService.LogAsync("user-3", "test3@example.com", "DELETE", "Product", "3");
        await _auditService.LogAsync("user-4", "test4@example.com", "CREATE", "Supplier", "4");

        // Act
        var result = (await _auditService.GetLogsAsync(entityType: "Product")).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.EntityType == "Product");
    }

    [Fact]
    public async Task GetLogsAsync_ShouldApplyPagination()
    {
        // Arrange
        await CreateTestLogs(25);

        // Act - Get first page
        var page1 = (await _auditService.GetLogsAsync(pageSize: 10, page: 1)).ToList();
        var page2 = (await _auditService.GetLogsAsync(pageSize: 10, page: 2)).ToList();
        var page3 = (await _auditService.GetLogsAsync(pageSize: 10, page: 3)).ToList();

        // Assert
        page1.Should().HaveCount(10);
        page2.Should().HaveCount(10);
        page3.Should().HaveCount(5);
        
        // Ensure no duplicates between pages
        var page1Ids = page1.Select(l => l.Id).ToList();
        var page2Ids = page2.Select(l => l.Id).ToList();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldOrderByTimestampDescending()
    {
        // Arrange
        await _auditService.LogAsync("user-1", "test1@example.com", "CREATE", "Product", "1");
        await Task.Delay(100);
        await _auditService.LogAsync("user-2", "test2@example.com", "UPDATE", "Product", "2");
        await Task.Delay(100);
        await _auditService.LogAsync("user-3", "test3@example.com", "DELETE", "Product", "3");

        // Act
        var result = (await _auditService.GetLogsAsync()).ToList();

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(3);
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].Timestamp.Should().BeOnOrAfter(result[i + 1].Timestamp);
        }
    }

    [Fact]
    public async Task GetLogsAsync_ShouldCombineMultipleFilters()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddMinutes(-2);
        await _auditService.LogAsync("user-target", "target@example.com", "CREATE", "Product", "1");
        await _auditService.LogAsync("user-target", "target@example.com", "UPDATE", "Order", "2");
        await _auditService.LogAsync("user-other", "other@example.com", "CREATE", "Product", "3");
        var endDate = DateTime.UtcNow;

        // Act
        var result = (await _auditService.GetLogsAsync(
            startDate: startDate,
            endDate: endDate,
            userId: "user-target",
            entityType: "Product")).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be("user-target");
        result.First().EntityType.Should().Be("Product");
    }

    [Fact]
    public async Task GetLogsAsync_ShouldReturnEmptyList_WhenNoMatchingLogs()
    {
        // Arrange
        await CreateTestLogs(5);

        // Act
        var result = (await _auditService.GetLogsAsync(userId: "non-existent-user")).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogsAsync_ShouldDefaultTo100PerPage()
    {
        // Arrange
        await CreateTestLogs(150);

        // Act - Don't specify page size
        var result = (await _auditService.GetLogsAsync()).ToList();

        // Assert
        result.Should().HaveCount(100);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestLogs(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await _auditService.LogAsync(
                $"user-{i}",
                $"test{i}@example.com",
                i % 3 == 0 ? "CREATE" : i % 3 == 1 ? "UPDATE" : "DELETE",
                i % 2 == 0 ? "Product" : "Order",
                $"ENTITY-{i}");
            
            // Small delay to ensure different timestamps
            if (i % 10 == 0)
                await Task.Delay(10);
        }
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
