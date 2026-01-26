using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Domain.Entities;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.DTOs;

namespace StatStock.Web.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all products with optional filtering
    /// </summary>
    /// <param name="category">Filter by category</param>
    /// <param name="search">Search by SKU or name</param>
    /// <param name="minStock">Filter by minimum stock quantity</param>
    /// <param name="maxStock">Filter by maximum stock quantity</param>
    /// <returns>List of products</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetProducts(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] int? minStock = null,
        [FromQuery] int? maxStock = null)
    {
        try
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.SKU.Contains(search) || p.Name.Contains(search));
            }

            if (minStock.HasValue)
            {
                query = query.Where(p => p.StockQuantity >= minStock.Value);
            }

            if (maxStock.HasValue)
            {
                query = query.Where(p => p.StockQuantity <= maxStock.Value);
            }

            var products = await query
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    SKU = p.SKU,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Category = p.Category,
                    ReorderLevel = p.ReorderLevel,
                    StockQuantity = p.StockQuantity,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<List<ProductDto>>.SuccessResult(products));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products");
            return StatusCode(500, ApiResponse<List<ProductDto>>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Product details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProduct(int id)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(ApiResponse<ProductDto>.ErrorResult("Product not found"));
            }

            var productDto = new ProductDto
            {
                Id = product.Id,
                SKU = product.SKU,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Category = product.Category,
                ReorderLevel = product.ReorderLevel,
                StockQuantity = product.StockQuantity,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            return Ok(ApiResponse<ProductDto>.SuccessResult(productDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Create a new product
    /// </summary>
    /// <param name="createDto">Product details</param>
    /// <returns>Created product</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct([FromBody] CreateProductDto createDto)
    {
        try
        {
            // Check if SKU already exists
            if (await _context.Products.AnyAsync(p => p.SKU == createDto.SKU))
            {
                return BadRequest(ApiResponse<ProductDto>.ErrorResult("Product with this SKU already exists"));
            }

            var product = new Product
            {
                SKU = createDto.SKU,
                Name = createDto.Name,
                Description = createDto.Description,
                Price = createDto.Price,
                Category = createDto.Category,
                ReorderLevel = createDto.ReorderLevel,
                StockQuantity = createDto.StockQuantity,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var productDto = new ProductDto
            {
                Id = product.Id,
                SKU = product.SKU,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Category = product.Category,
                ReorderLevel = product.ReorderLevel,
                StockQuantity = product.StockQuantity,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            _logger.LogInformation("Product created: {SKU}", product.SKU);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, ApiResponse<ProductDto>.SuccessResult(productDto, "Product created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, ApiResponse<ProductDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Update an existing product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="updateDto">Updated product details</param>
    /// <returns>Updated product</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, [FromBody] UpdateProductDto updateDto)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(ApiResponse<ProductDto>.ErrorResult("Product not found"));
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(updateDto.SKU))
            {
                // Check if new SKU already exists (excluding current product)
                if (await _context.Products.AnyAsync(p => p.SKU == updateDto.SKU && p.Id != id))
                {
                    return BadRequest(ApiResponse<ProductDto>.ErrorResult("Product with this SKU already exists"));
                }
                product.SKU = updateDto.SKU;
            }

            if (!string.IsNullOrEmpty(updateDto.Name))
                product.Name = updateDto.Name;

            if (!string.IsNullOrEmpty(updateDto.Description))
                product.Description = updateDto.Description;

            if (updateDto.Price.HasValue)
                product.Price = updateDto.Price.Value;

            if (!string.IsNullOrEmpty(updateDto.Category))
                product.Category = updateDto.Category;

            if (updateDto.ReorderLevel.HasValue)
                product.ReorderLevel = updateDto.ReorderLevel.Value;

            if (updateDto.StockQuantity.HasValue)
                product.StockQuantity = updateDto.StockQuantity.Value;

            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var productDto = new ProductDto
            {
                Id = product.Id,
                SKU = product.SKU,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Category = product.Category,
                ReorderLevel = product.ReorderLevel,
                StockQuantity = product.StockQuantity,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            _logger.LogInformation("Product updated: {Id}", id);
            return Ok(ApiResponse<ProductDto>.SuccessResult(productDto, "Product updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {Id}", id);
            return StatusCode(500, ApiResponse<ProductDto>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Delete a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Success message</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(int id)
    {
        try
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Product not found"));
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product deleted: {Id}", id);
            return Ok(ApiResponse<object>.SuccessResult(new { id }, "Product deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get all product categories
    /// </summary>
    /// <returns>List of categories</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        try
        {
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(ApiResponse<List<string>>.SuccessResult(categories));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories");
            return StatusCode(500, ApiResponse<List<string>>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }

    /// <summary>
    /// Get products with low stock (below reorder level)
    /// </summary>
    /// <returns>List of low stock products</returns>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetLowStockProducts()
    {
        try
        {
            var products = await _context.Products
                .Where(p => p.StockQuantity <= p.ReorderLevel)
                .OrderBy(p => p.StockQuantity)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    SKU = p.SKU,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Category = p.Category,
                    ReorderLevel = p.ReorderLevel,
                    StockQuantity = p.StockQuantity,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<List<ProductDto>>.SuccessResult(products));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching low stock products");
            return StatusCode(500, ApiResponse<List<ProductDto>>.ErrorResult("Internal server error", new List<string> { ex.Message }));
        }
    }
}
