using Microsoft.EntityFrameworkCore;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;

namespace StatStock.Infrastructure.Data.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Seed Products
        await SeedProductsAsync(context);
        
        // Seed Suppliers
        await SeedSuppliersAsync(context);
        
        // Note: Orders seeding disabled because it requires users
        // Uncomment when user management is implemented
        await SeedOrdersAsync(context);
        
        await context.SaveChangesAsync();
    }


    private static async Task SeedProductsAsync(ApplicationDbContext context)
    {
        if (await context.Products.AnyAsync())
            return;

        var products = new List<Product>
        {
            // Electronics
            new Product { SKU = "ELEC-LAP-001", Name = "Business Laptop", Description = "15-inch business laptop with Intel i7", Price = 1299.99m, Category = "Electronics", ReorderLevel = 5, StockQuantity = 15 },
            new Product { SKU = "ELEC-MON-001", Name = "27-inch Monitor", Description = "4K UHD Monitor", Price = 399.99m, Category = "Electronics", ReorderLevel = 10, StockQuantity = 25 },
            new Product { SKU = "ELEC-KEY-001", Name = "Wireless Keyboard", Description = "Ergonomic wireless keyboard", Price = 79.99m, Category = "Electronics", ReorderLevel = 20, StockQuantity = 50 },
            new Product { SKU = "ELEC-MOU-001", Name = "Wireless Mouse", Description = "Precision wireless mouse", Price = 49.99m, Category = "Electronics", ReorderLevel = 20, StockQuantity = 60 },
            
            // Office Furniture
            new Product { SKU = "FURN-CHA-001", Name = "Ergonomic Desk Chair", Description = "Adjustable office chair with lumbar support", Price = 349.99m, Category = "Furniture", ReorderLevel = 5, StockQuantity = 12 },
            new Product { SKU = "FURN-DSK-001", Name = "Standing Desk", Description = "Height-adjustable standing desk", Price = 599.99m, Category = "Furniture", ReorderLevel = 3, StockQuantity = 8 },
            new Product { SKU = "FURN-CAB-001", Name = "Filing Cabinet", Description = "4-drawer filing cabinet", Price = 249.99m, Category = "Furniture", ReorderLevel = 5, StockQuantity = 10 },
            
            // Office Supplies
            new Product { SKU = "SUPP-PAP-001", Name = "Copy Paper (Ream)", Description = "500 sheets of A4 copy paper", Price = 7.99m, Category = "Supplies", ReorderLevel = 50, StockQuantity = 150 },
            new Product { SKU = "SUPP-INK-001", Name = "Ink Cartridge - Black", Description = "High-yield black ink cartridge", Price = 49.99m, Category = "Supplies", ReorderLevel = 20, StockQuantity = 45 },
            new Product { SKU = "SUPP-INK-002", Name = "Ink Cartridge - Color", Description = "High-yield color ink cartridge", Price = 54.99m, Category = "Supplies", ReorderLevel = 20, StockQuantity = 40 },
            new Product { SKU = "SUPP-NOT-001", Name = "Spiral Notebooks", Description = "Pack of 5 college-ruled notebooks", Price = 12.99m, Category = "Supplies", ReorderLevel = 30, StockQuantity = 80 },
            new Product { SKU = "SUPP-PEN-001", Name = "Ballpoint Pens (Box)", Description = "Box of 50 blue ballpoint pens", Price = 15.99m, Category = "Supplies", ReorderLevel = 25, StockQuantity = 70 },
        };

        await context.Products.AddRangeAsync(products);
    }

    private static async Task SeedSuppliersAsync(ApplicationDbContext context)
    {
        if (await context.Suppliers.AnyAsync())
            return;

        var suppliers = new List<Supplier>
        {
            new Supplier 
            { 
                Name = "TechWholesale Inc.", 
                Contact = "John Smith", 
                Email = "sales@techwholesale.com", 
                Phone = "+1-555-0100", 
                Address = "123 Tech Avenue, Silicon Valley, CA 94025" 
            },
            new Supplier 
            { 
                Name = "Office Depot Pro", 
                Contact = "Sarah Johnson", 
                Email = "corporate@officedepot.com", 
                Phone = "+1-555-0200", 
                Address = "456 Business Parkway, New York, NY 10001" 
            },
            new Supplier 
            { 
                Name = "Global Parts Ltd.", 
                Contact = "Michael Chen", 
                Email = "orders@globalparts.com", 
                Phone = "+1-555-0300", 
                Address = "789 Industrial Drive, Los Angeles, CA 90001" 
            },
            new Supplier 
            { 
                Name = "FurniturePro Supply", 
                Contact = "Emily Rodriguez", 
                Email = "info@furniturepro.com", 
                Phone = "+1-555-0400", 
                Address = "321 Commerce Street, Chicago, IL 60601" 
            },
        };

        await context.Suppliers.AddRangeAsync(suppliers);
    }

    private static async Task SeedOrdersAsync(ApplicationDbContext context)
    {
        var testOrderNumbers = new[] { "ORD-20260125-001", "ORD-20260120-002", "ORD-20260118-003", "ORD-20260115-004" };
        
        // Check if any of our test orders already exist
        var existingTestOrders = await context.Orders
            .Where(o => testOrderNumbers.Contains(o.OrderNumber))
            .Include(o => o.Items)  // Include items for deletion
            .ToListAsync();

        // If we have all test orders with correct status, skip seeding
        var allCorrectStatus = existingTestOrders
            .Where(o => o.OrderNumber == "ORD-20260118-003" || o.OrderNumber == "ORD-20260115-004")
            .All(o => (o.OrderNumber == "ORD-20260118-003" && o.Status == OrderStatus.Approved) ||
                      (o.OrderNumber == "ORD-20260115-004" && o.Status == OrderStatus.Delivered))
            && existingTestOrders
            .Where(o => o.OrderNumber == "ORD-20260125-001" || o.OrderNumber == "ORD-20260120-002")
            .All(o => o.Status == OrderStatus.Pending);
        
        if (existingTestOrders.Count == testOrderNumbers.Length && allCorrectStatus)
            return;

        // Remove any existing test orders to allow fresh seeding
        if (existingTestOrders.Any())
        {
            // Remove items first (cascade delete should handle this, but being explicit)
            var itemsToDelete = existingTestOrders.SelectMany(o => o.Items).ToList();
            foreach (var item in itemsToDelete)
            {
                context.OrderItems.Remove(item);
            }
            context.Orders.RemoveRange(existingTestOrders);
            await context.SaveChangesAsync();
        }

        var orders = new List<Order>
        {
            new Order
            {
                OrderNumber = "ORD-20260125-001",
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending,  // ← Key: Not approved
                CreatedAt = DateTime.Now.AddDays(-5),
                ApprovedAt = null,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 1, Quantity = 15, UnitPrice = 60m }
                },
                SupplierId = 4,  // Changed from 1 (TechWholesale) to 4 (FurniturePro) - not a trusted supplier
                UserId = "1"
            },

            new Order
            {
                OrderNumber = "ORD-20260120-002",
                Type = OrderType.Incoming,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.Now.AddDays(-10),
                ApprovedAt = null,
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 3, Quantity = 50, UnitPrice = 20.00m }  // Reduced from 400 to 20 for total $1000 > $500, not auto-approved by Rule 1
                },
                SupplierId = 3,  // Changed to Global Parts Ltd. - not a trusted supplier
                UserId = "1"
            },

            // Approved orders (for status filter testing)
            new Order
            {
                OrderNumber = "ORD-20260118-003",
                Type = OrderType.Incoming,
                Status = OrderStatus.Approved,
                CreatedAt = DateTime.Now.AddDays(-15),
                ApprovedAt = DateTime.Now.AddDays(-14),
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 2, Quantity = 5, UnitPrice = 199.99m }
                },
                SupplierId = 1,
                UserId = "1"
            },

            // Delivered orders (for different status testing)
            new Order
            {
                OrderNumber = "ORD-20260115-004",
                Type = OrderType.Outgoing,
                Status = OrderStatus.Delivered,
                CreatedAt = DateTime.Now.AddDays(-20),
                ApprovedAt = DateTime.Now.AddDays(-19),
                Items = new List<OrderItem>
                {
                    new OrderItem { ProductId = 4, Quantity = 3, UnitPrice = 450.00m }
                },
                SupplierId = 3,
                UserId = "1"
            },
        };
        await context.Orders.AddRangeAsync(orders);
    }
}

