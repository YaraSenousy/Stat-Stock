using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StatStock.Infrastructure.Data;
using StatStock.Infrastructure.Data.Seeders;
using StatStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllersWithViews();

    // Add DbContext
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Identity temporarily disabled due to .NET 10 preview package incompatibilities
    // The Microsoft.AspNetCore.Identity.EntityFrameworkCore package has breaking changes
    // that cause TypeLoadException: Missing 'SetPasskeyAsync' method in UserStore
    // TODO: Re-enable when using .NET 8/9 or when .NET 10 packages are stable
    
    /* 
    builder.Services.AddIdentity<ApplicationIdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
    */

    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() 
        {
            Title = "StatStock API",
            Version = "v1",
            Description = "Inventory Management Platform API"
        });
    });

    var app = builder.Build();

    // Seed database
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetService<UserManager<ApplicationIdentityUser>>();
            var roleManager = services.GetService<RoleManager<IdentityRole>>();
            
            // Apply migrations
            await context.Database.MigrateAsync();
            
            // Seed data only when Identity services are available (registration disabled in this run)
            if (userManager is not null && roleManager is not null)
            {
                await DataSeeder.SeedAsync(context, userManager, roleManager);
                Log.Information("Database seeded successfully");
            }
            else
            {
                Log.Warning("Identity services are not registered; skipping data seeding.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database");
        }
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "StatStock API V1");
        });
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    // Authentication/Authorization disabled while Identity is disabled
    // app.UseAuthentication();
    // app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    Log.Information("Starting StatStock application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
