using Microsoft.EntityFrameworkCore;
using Serilog;
using StatStock.Infrastructure.Data;
using StatStock.Infrastructure.Data.Seeders;
using StatStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

    // Add SignalR for real-time updates
    builder.Services.AddSignalR();

    // Add DbContext - Use SQLite for Linux/testing, SQL Server for Windows/production
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (OperatingSystem.IsWindows())
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
    }
    else
    {
        // Use SQLite for non-Windows environments (Linux/Mac)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite("Data Source=StatStock.db"));
        Log.Information("Using SQLite database for non-Windows environment");
    }

    // Add Authentication: Cookie for MVC + JWT for API
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "ReplaceThisWithSecretKey123!";
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

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

    // Enable authentication and authorization (Cookie for MVC, JWT for API)
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    // Map SignalR Hub
    app.MapHub<StatStock.Web.Hubs.DashboardHub>("/dashboardHub");

    // Add area route
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

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
