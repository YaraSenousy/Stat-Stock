using Microsoft.EntityFrameworkCore;
using Serilog;
using StatStock.Infrastructure.Data;
using StatStock.Infrastructure.Data.Seeders;
using StatStock.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StatStock.Web.Api.Services;
using StatStock.Web.Api.Middleware;
using StatStock.Application.Interfaces;
using StatStock.Infrastructure.Services;

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
        {
            options.UseSqlServer(connectionString);
            // Suppress pending model changes warning
            options.ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
    }
    else
    {
        // Use SQLite for non-Windows environments (Linux/Mac)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlite("Data Source=StatStock.db");
            // Suppress pending model changes warning
            options.ConfigureWarnings(warnings => 
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        Log.Information("Using SQLite database for non-Windows environment");
    }

    // Configure Authentication (Custom implementation without ASP.NET Identity)
    // Using cookie-based authentication for MVC and JWT for API
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "ReplaceThisWithSecretKey123!";
    
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
        options.Cookie.Name = "StatStockAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

    // Register API services
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IWebhookService, WebhookService>();
    builder.Services.AddScoped<IReportService, ReportService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<ICustomUserService, CustomUserService>();
    builder.Services.AddHttpClient();

    // Add Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() 
        {
            Title = "StatStock API",
            Version = "v1",
            Description = "Inventory Management Platform API for B2B clients. Use /api/auth/token endpoint to get JWT token, then use 'Authorize' button with 'Bearer {token}' format."
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
            var userService = services.GetRequiredService<ICustomUserService>();
            
            // Apply migrations
            await context.Database.MigrateAsync();
            
            // Seed data with custom user service for user creation
            await DataSeeder.SeedAsync(context, userService);
            Log.Information("Database seeded successfully");
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

    // Enable rate limiting for API endpoints
    app.UseRateLimiting();

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
