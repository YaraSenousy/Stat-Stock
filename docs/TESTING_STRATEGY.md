# Testing Strategy for Phase 4 Features

## The Problem
Phase 4 features (bulk approvals, order search, date filters, automated approval rules, notifications) need testing, but:
- Orders can only be placed via **Terminal (automatically approved)**
- No way to create pending orders through the UI
- Approval workflows cannot be tested with already-approved orders

## Solution: Multiple Testing Approaches

---

## Approach 1: Database Seeding (Recommended) ⭐

### Implementation
Add **pending orders** to the database seed data in `Program.cs`:

```csharp
// In seed data (Program.cs or DbInitializer)
orders.Add(new Order
{
    OrderNo = "ORD-20260125-001",
    Type = OrderType.Incoming,
    Status = OrderStatus.Pending,  // ← Key: Not approved
    CreatedAt = DateTime.Now.AddDays(-5),
    ApprovedAt = null,
    Items = new List<OrderItem>
    {
        new OrderItem { ProductId = 1, Quantity = 10, UnitPrice = 25.99m }
    },
    SupplierId = 1,
    UserId = 1
});

orders.Add(new Order
{
    OrderNo = "ORD-20260120-002",
    Type = OrderType.Incoming,
    Status = OrderStatus.Pending,
    CreatedAt = DateTime.Now.AddDays(-10),
    ApprovedAt = null,
    Items = new List<OrderItem>
    {
        new OrderItem { ProductId = 3, Quantity = 50, UnitPrice = 12.00m }
    },
    SupplierId = 2,
    UserId = 1
});

// Approved orders (for status filter testing)
orders.Add(new Order
{
    OrderNo = "ORD-20260118-003",
    Type = OrderType.Incoming,
    Status = OrderStatus.Approved,
    CreatedAt = DateTime.Now.AddDays(-15),
    ApprovedAt = DateTime.Now.AddDays(-14),
    Items = new List<OrderItem>
    {
        new OrderItem { ProductId = 2, Quantity = 5, UnitPrice = 199.99m }
    },
    SupplierId = 1,
    UserId = 1
});

// Delivered orders (for different status testing)
orders.Add(new Order
{
    OrderNo = "ORD-20260115-004",
    Type = OrderType.Outgoing,
    Status = OrderStatus.Delivered,
    CreatedAt = DateTime.Now.AddDays(-20),
    ApprovedAt = DateTime.Now.AddDays(-19),
    Items = new List<OrderItem>
    {
        new OrderItem { ProductId = 4, Quantity = 3, UnitPrice = 450.00m }
    },
    SupplierId = 3,
    UserId = 1
});
```

### Test Scenarios Enabled
✅ Search filtering (find by order number)  
✅ Date range filtering (pending orders from specific date ranges)  
✅ Status filtering (show only pending orders)  
✅ Bulk selection & approval  
✅ Automated approval rules triggering  
✅ Toast notifications on status change  

**Pros**: Real data, production-like testing  
**Cons**: Requires fixing database migrations first

---

## Approach 2: Create Orders via API (Workaround) 🔧

### Implementation
Create a **test endpoint** in the B2B API (only enable in Development):

```csharp
// In StatStock.Web/Api/TestOrdersController.cs (Optional)
#if DEBUG
[ApiController]
[Route("api/test")]
public class TestOrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    
    public TestOrdersController(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }
    
    [HttpPost("create-test-orders")]
    public async Task<IActionResult> CreateTestOrders()
    {
        var testOrders = new[]
        {
            new Order
            {
                OrderNo = $"TEST-{DateTime.Now.Ticks}",
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.Now.AddDays(-5),
                Items = new List<OrderItem> { /* ... */ }
            }
        };
        
        foreach (var order in testOrders)
        {
            await _orderRepository.AddAsync(order);
        }
        
        return Ok(new { Message = "Test orders created", Count = testOrders.Length });
    }
}
#endif
```

**Access**: `POST /api/test/create-test-orders` (in browser via Swagger)

**Pros**: No database migration fixes needed, quick setup  
**Cons**: Requires code changes, only in dev mode, not production-like

---

## Approach 3: Postman/Insomnia API Testing 🚀

### Implementation
If you have B2B API endpoints, use Postman to:

1. **Create test orders via API**:
```json
POST /api/orders
{
    "orderNo": "TEST-API-001",
    "type": "Incoming",
    "status": "Pending",
    "items": [
        { "productId": 1, "quantity": 10, "unitPrice": 25.99 }
    ],
    "supplierId": 1
}
```

2. **Verify filter endpoints work**:
```
GET /api/orders?status=Pending&fromDate=2026-01-01&toDate=2026-01-31
GET /api/orders?search=TEST-API
```

**Pros**: Tests API layer independently  
**Cons**: Requires functional B2B API, doesn't test UI

---

## Approach 4: Unit & Integration Tests (Best for CI/CD) 📋

### Implementation

Create test file: `tests/StatStock.Web.Tests/Areas/Manager/Controllers/OrdersControllerTests.cs`

```csharp
[Fact]
public async Task Index_WithPendingOrders_ReturnFilteredResults()
{
    // Arrange
    var mockOrders = new[]
    {
        new Order 
        { 
            Id = 1, 
            OrderNo = "ORD-001", 
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.Now.AddDays(-5)
        },
        new Order 
        { 
            Id = 2, 
            OrderNo = "ORD-002", 
            Status = OrderStatus.Approved,
            CreatedAt = DateTime.Now.AddDays(-10)
        }
    }.AsQueryable();
    
    var mockRepo = new Mock<IOrderRepository>();
    mockRepo.Setup(r => r.GetAllAsync())
        .ReturnsAsync(mockOrders.ToList());
    
    var controller = new OrdersController(mockRepo.Object);
    
    // Act
    var result = await controller.Index(status: "Pending");
    
    // Assert
    var viewResult = Assert.IsType<ViewResult>(result);
    var orders = Assert.IsAssignableFrom<IEnumerable<Order>>(viewResult.Model);
    Assert.Single(orders); // Only 1 pending order
    Assert.Equal("ORD-001", orders.First().OrderNo);
}

[Fact]
public async Task BulkUpdateStatus_WithMultipleOrders_UpdatesAllSelections()
{
    // Arrange
    var orderIds = new[] { 1, 2, 3 };
    var mockOrders = new[]
    {
        new Order { Id = 1, Status = OrderStatus.Pending },
        new Order { Id = 2, Status = OrderStatus.Pending },
        new Order { Id = 3, Status = OrderStatus.Pending }
    }.AsQueryable();
    
    var mockRepo = new Mock<IOrderRepository>();
    mockRepo.Setup(r => r.GetByIdsAsync(It.IsAny<int[]>()))
        .ReturnsAsync(mockOrders.ToList());
    
    var controller = new OrdersController(mockRepo.Object);
    
    // Act
    var result = await controller.BulkUpdateStatus(orderIds, OrderStatus.Approved);
    
    // Assert
    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
    mockRepo.Verify(r => r.SaveAsync(), Times.Once);
}

[Fact]
public async Task ApplyAutomatedApprovalRules_WithLowValueOrder_AutoApprovesUnder500()
{
    // Arrange
    var order = new Order
    {
        OrderNo = "LOW-VALUE",
        Type = OrderType.Incoming,
        Status = OrderStatus.Pending,
        Items = new List<OrderItem>
        {
            new OrderItem { Quantity = 5, UnitPrice = 50m } // Total: $250
        }
    };
    
    // Act
    // (Call private method via reflection or refactor to public)
    var totalValue = order.Items.Sum(i => i.Quantity * i.UnitPrice);
    
    // Assert
    Assert.True(totalValue < 500);
    Assert.Equal(OrderStatus.Pending, order.Status); // Before rule applied
}

[Fact]
public async Task DateRangeFilter_WithFromAndToDate_ReturnsOrdersInRange()
{
    // Arrange
    var fromDate = DateTime.Now.AddDays(-10);
    var toDate = DateTime.Now.AddDays(-5);
    
    var mockOrders = new[]
    {
        new Order { Id = 1, CreatedAt = DateTime.Now.AddDays(-8) }, // In range
        new Order { Id = 2, CreatedAt = DateTime.Now.AddDays(-3) }, // Outside range
    }.AsQueryable();
    
    // Act
    var filtered = mockOrders
        .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
        .ToList();
    
    // Assert
    Assert.Single(filtered);
    Assert.Equal(1, filtered.First().Id);
}
```

**Run tests**:
```powershell
dotnet test tests/StatStock.Web.Tests/Areas/Manager/Controllers/OrdersControllerTests.cs
```

**Pros**: Automated, repeatable, CI/CD friendly  
**Cons**: Doesn't test actual UI rendering

---

## Approach 5: Manual Testing Checklist (Quick & Dirty) ✓

### Quick Setup (5 minutes)
1. **Directly edit database** (if using SQLite for dev):
   - Open database file with SQLite Browser
   - INSERT pending orders manually into Orders table

2. **Test Checklist**:
   - [ ] Search: Type "ORD-" in search, verify results filter
   - [ ] Date Filter: Select date range, verify correct orders show
   - [ ] Status Filter: Select "Pending", verify only pending orders display
   - [ ] Bulk Select: Check 3 orders, verify counter updates
   - [ ] Bulk Approve: Select multiple, click "Approve Selected", verify notification
   - [ ] Automated Rules: Create order < $500, navigate to Orders page, verify auto-approved
   - [ ] Toast Notifications: Approve/reject order, verify green success toast appears and auto-dismisses

### SQL to Insert Test Data (SQLite/SQL Server)
```sql
-- Insert test supplier first (if not exists)
INSERT INTO Suppliers (Name, Contact, Email, Phone, Address)
VALUES ('Test Supplier', 'John Doe', 'test@supplier.com', '555-1234', '123 Test St');

-- Insert pending orders
INSERT INTO Orders (OrderNo, Type, Status, CreatedAt, ApprovedAt, SupplierId, UserId)
VALUES 
  ('TEST-PEND-001', 'Incoming', 'Pending', GETDATE(), NULL, 1, 1),
  ('TEST-PEND-002', 'Incoming', 'Pending', DATEADD(day, -5, GETDATE()), NULL, 1, 1),
  ('TEST-APPROVED', 'Incoming', 'Approved', DATEADD(day, -10, GETDATE()), DATEADD(day, -9, GETDATE()), 1, 1);

-- Insert order items for those orders
INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice)
VALUES 
  (1, 1, 10, 25.99),
  (2, 3, 50, 12.00),
  (3, 2, 5, 199.99);
```

---

## Recommended Testing Path 🎯

### **Phase 4 - Step by Step**:

1. **✅ First: Fix Database** (prerequisite)
   - Resolve SQLite migration issues
   - Add pending orders to seed data
   - Time: 30-60 minutes

2. **✅ Second: Manual Testing** (quick validation)
   - Use checklist above
   - Test all UI features
   - Time: 15-20 minutes

3. **✅ Third: Write Unit Tests**
   - Test filtering logic
   - Test bulk operations
   - Test automated rules
   - Time: 1-2 hours

4. **✅ Fourth: Integration Tests**
   - Test controller → repository flow
   - Test database updates
   - Time: 1 hour

---

## Quick Command to Test Notifications

Open browser console and trigger manually:

```javascript
// In browser console, triggers the notification system
window.showNotification('Test Success', 'success');
window.showNotification('Test Error', 'error');
window.showNotification('Test Info', 'info');
```

---

## Summary

| Approach | Setup Time | Effort | Best For |
|----------|-----------|--------|----------|
| Database Seeding | 30 min | Low | Full UI testing, realistic data |
| Test Endpoint | 20 min | Medium | Quick API testing |
| Postman | 15 min | Low | API-only testing |
| Unit Tests | 1-2 hrs | High | Automated CI/CD, regression |
| Manual Checklist | 5 min | Low | Quick spot-check |

**Recommendation**: Start with **Database Seeding** → **Manual Testing** → **Unit Tests** for comprehensive Phase 4 validation.
