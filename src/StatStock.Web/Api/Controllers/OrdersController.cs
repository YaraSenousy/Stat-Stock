using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.DTOs;
using StatStock.Web.Api.Services;

namespace StatStock.Web.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrdersController> _logger;
    private readonly IWebhookService _webhookService;

    public OrdersController(
        ApplicationDbContext context, 
        ILogger<OrdersController> logger,
        IWebhookService webhookService)
    {
        _context = context;
        _logger = logger;
        _webhookService = webhookService;
    }

    /// <summary>
    /// Get all orders with optional filtering
    /// </summary>
    /// <param name="status">Filter by order status</param>
    /// <param name="type">Filter by order type</param>
    /// <param name="fromDate">Filter from date</param>
    /// <param name="toDate">Filter to date</param>
    /// <returns>List of orders</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetOrders(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] OrderType? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var query = _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(o => o.Type == type.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(o => o.CreatedAt <= endDate);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Type = o.Type,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    ApprovedAt = o.ApprovedAt,
                    Notes = o.Notes,
                    SupplierId = o.SupplierId,
                    SupplierName = o.Supplier != null ? o.Supplier.Name : string.Empty,
                    UserId = o.UserId,
                    Items = o.Items.Select(i => new OrderItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product.Name,
                        ProductSKU = i.Product.SKU,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Subtotal = i.Quantity * i.UnitPrice
                    }).ToList(),
                    TotalAmount = o.Items.Sum(i => i.Quantity * i.UnitPrice)
                })
                .ToListAsync();

            return Ok(ApiResponse<List<OrderDto>>.SuccessResult(orders));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders");
            return StatusCode(500, ApiResponse<List<OrderDto>>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get a specific order by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>Order details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(int id)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(ApiResponse<OrderDto>.ErrorResult("Order not found"));
            }

            var orderDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Type = order.Type,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                ApprovedAt = order.ApprovedAt,
                Notes = order.Notes,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier?.Name ?? string.Empty,
                UserId = order.UserId,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Quantity * i.UnitPrice
                }).ToList(),
                TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice)
            };

            return Ok(ApiResponse<OrderDto>.SuccessResult(orderDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order {Id}", id);
            return StatusCode(500, ApiResponse<OrderDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    /// <param name="createDto">Order details</param>
    /// <returns>Created order</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder([FromBody] CreateOrderDto createDto)
    {
        try
        {
            if (createDto.Items == null || !createDto.Items.Any())
            {
                return BadRequest(ApiResponse<OrderDto>.ErrorResult("Order must have at least one item"));
            }

            // Validate products exist
            var productIds = createDto.Items.Select(i => i.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                return BadRequest(ApiResponse<OrderDto>.ErrorResult("One or more products not found"));
            }

            // Get userId from JWT claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "api-user";

            // Generate order number
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}";

            var order = new Order
            {
                OrderNumber = orderNumber,
                Type = createDto.Type,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Notes = createDto.Notes,
                SupplierId = createDto.SupplierId,
                UserId = userId,
                Items = createDto.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Load navigation properties
            await _context.Entry(order)
                .Collection(o => o.Items)
                .Query()
                .Include(i => i.Product)
                .LoadAsync();

            if (order.SupplierId.HasValue)
            {
                await _context.Entry(order).Reference(o => o.Supplier).LoadAsync();
            }

            var orderDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Type = order.Type,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                ApprovedAt = order.ApprovedAt,
                Notes = order.Notes,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier?.Name ?? string.Empty,
                UserId = order.UserId,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Quantity * i.UnitPrice
                }).ToList(),
                TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice)
            };

            _logger.LogInformation("Order created: {OrderNumber}", order.OrderNumber);
            
            // Send webhook notification
            await _webhookService.NotifyOrderCreated(orderDto);

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, ApiResponse<OrderDto>.SuccessResult(orderDto, "Order created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, ApiResponse<OrderDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update order status
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="updateDto">New status</param>
    /// <returns>Updated order</returns>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto updateDto)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(ApiResponse<OrderDto>.ErrorResult("Order not found"));
            }

            var oldStatus = order.Status;
            order.Status = updateDto.Status;

            if (updateDto.Status == OrderStatus.Approved && !order.ApprovedAt.HasValue)
            {
                order.ApprovedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var orderDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Type = order.Type,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                ApprovedAt = order.ApprovedAt,
                Notes = order.Notes,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier?.Name ?? string.Empty,
                UserId = order.UserId,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Quantity * i.UnitPrice
                }).ToList(),
                TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice)
            };

            _logger.LogInformation("Order {OrderNumber} status updated from {OldStatus} to {NewStatus}", order.OrderNumber, oldStatus, order.Status);
            
            // Send webhook notification
            await _webhookService.NotifyOrderStatusChanged(orderDto, oldStatus, order.Status);

            return Ok(ApiResponse<OrderDto>.SuccessResult(orderDto, "Order status updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status {Id}", id);
            return StatusCode(500, ApiResponse<OrderDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>Success message</returns>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CancelOrder(int id)
    {
        try
        {
            var order = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(ApiResponse<OrderDto>.ErrorResult("Order not found"));
            }

            if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
            {
                return BadRequest(ApiResponse<OrderDto>.ErrorResult($"Cannot cancel order with status {order.Status}"));
            }

            var oldStatus = order.Status;
            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            var orderDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Type = order.Type,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                ApprovedAt = order.ApprovedAt,
                Notes = order.Notes,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier?.Name ?? string.Empty,
                UserId = order.UserId,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ProductSKU = i.Product.SKU,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Quantity * i.UnitPrice
                }).ToList(),
                TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice)
            };

            _logger.LogInformation("Order {OrderNumber} cancelled", order.OrderNumber);
            
            // Send webhook notification
            await _webhookService.NotifyOrderStatusChanged(orderDto, oldStatus, OrderStatus.Cancelled);

            return Ok(ApiResponse<OrderDto>.SuccessResult(orderDto, "Order cancelled successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {Id}", id);
            return StatusCode(500, ApiResponse<OrderDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get order history for the current user
    /// </summary>
    /// <returns>List of user orders</returns>
    [HttpGet("my-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetMyOrders()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponse<List<OrderDto>>.ErrorResult("User ID not found in token"));
            }

            var orders = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    Type = o.Type,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    ApprovedAt = o.ApprovedAt,
                    Notes = o.Notes,
                    SupplierId = o.SupplierId,
                    SupplierName = o.Supplier != null ? o.Supplier.Name : string.Empty,
                    UserId = o.UserId,
                    Items = o.Items.Select(i => new OrderItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product.Name,
                        ProductSKU = i.Product.SKU,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Subtotal = i.Quantity * i.UnitPrice
                    }).ToList(),
                    TotalAmount = o.Items.Sum(i => i.Quantity * i.UnitPrice)
                })
                .ToListAsync();

            return Ok(ApiResponse<List<OrderDto>>.SuccessResult(orders));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user orders");
            return StatusCode(500, ApiResponse<List<OrderDto>>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }
}
