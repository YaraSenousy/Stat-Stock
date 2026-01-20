using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StatStock.Domain.Entities;
using StatStock.Domain.Enums;
using StatStock.Infrastructure.Identity;

namespace StatStock.Infrastructure.Data.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationIdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Seed Roles
        await SeedRolesAsync(roleManager);
        
        // Seed Users
        await SeedUsersAsync(userManager);
        
        // Seed Products
        await SeedProductsAsync(context);
        
        // Seed Suppliers
        await SeedSuppliersAsync(context);
        
        // Seed Orders
        await SeedOrdersAsync(context);
        
        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "Manager", "FloorStaff", "B2BClient" };
        
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationIdentityUser> userManager)
    {
        // Admin User
        if (await userManager.FindByEmailAsync("admin@statstock.com") == null)
        {
            var admin = new ApplicationIdentityUser
            {
                UserName = "admin@statstock.com",
                Email = "admin@statstock.com",
                FirstName = "Admin",
                LastName = "User",
                Role = UserRole.Admin,
                Area = "All",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Manager User
        if (await userManager.FindByEmailAsync("manager@statstock.com") == null)
        {
            var manager = new ApplicationIdentityUser
            {
                UserName = "manager@statstock.com",
                Email = "manager@statstock.com",
                FirstName = "Manager",
                LastName = "User",
                Role = UserRole.Manager,
                Area = "Warehouse A",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(manager, "Manager@123");
            await userManager.AddToRoleAsync(manager, "Manager");
        }

        // Floor Staff User
        if (await userManager.FindByEmailAsync("staff@statstock.com") == null)
        {
            var staff = new ApplicationIdentityUser
            {
                UserName = "staff@statstock.com",
                Email = "staff@statstock.com",
                FirstName = "Floor",
                LastName = "Staff",
                Role = UserRole.FloorStaff,
                Area = "Warehouse A",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(staff, "Staff@123");
            await userManager.AddToRoleAsync(staff, "FloorStaff");
        }

        // B2B Client User
        if (await userManager.FindByEmailAsync("client@statstock.com") == null)
        {
            var client = new ApplicationIdentityUser
            {
                UserName = "client@statstock.com",
                Email = "client@statstock.com",
                FirstName = "B2B",
                LastName = "Client",
                Role = UserRole.B2BClient,
                Area = "External",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(client, "Client@123");
            await userManager.AddToRoleAsync(client, "B2BClient");
        }
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
        if (await context.Orders.AnyAsync())
            return;

        var products = await context.Products.ToListAsync();
        var suppliers = await context.Suppliers.ToListAsync();
        var users = await context.Users.ToListAsync();

        if (!products.Any() || !suppliers.Any() || !users.Any())
            return;

        var random = new Random(42); // Fixed seed for consistency
        var orders = new List<Order>();

        // Create 25 sample orders with various statuses
        var statuses = new[] { OrderStatus.Pending, OrderStatus.Approved, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled };
        var types = new[] { OrderType.Incoming, OrderType.Outgoing };

        for (int i = 1; i <= 25; i++)
        {
            var status = statuses[random.Next(statuses.Length)];
            var type = types[random.Next(types.Length)];
            var createdDate = DateTime.UtcNow.AddDays(-random.Next(1, 60));
            
            var order = new Order
            {
                OrderNumber = $"ORD-{DateTime.UtcNow.Year}-{i:D4}",
                Type = type,
                Status = status,
                CreatedAt = createdDate,
                ApprovedAt = status != OrderStatus.Pending ? createdDate.AddHours(random.Next(1, 48)) : null,
                Notes = $"Sample order {i}",
                SupplierId = type == OrderType.Incoming ? suppliers[random.Next(suppliers.Count)].Id : null,
                UserId = users[random.Next(users.Count)].Id,
                Items = new List<OrderItem>()
            };

            // Add 1-5 items to each order
            int itemCount = random.Next(1, 6);
            for (int j = 0; j < itemCount; j++)
            {
                var product = products[random.Next(products.Count)];
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = random.Next(1, 20),
                    UnitPrice = product.Price
                });
            }

            orders.Add(order);
        }

        await context.Orders.AddRangeAsync(orders);
    }
}
