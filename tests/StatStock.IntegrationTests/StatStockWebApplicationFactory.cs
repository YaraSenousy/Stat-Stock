using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StatStock.Infrastructure.Data;
using System.Security.Claims;

namespace StatStock.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the test server with an in-memory database and bypasses authentication.
/// </summary>
public class StatStockWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext registrations - must remove all variations
            var dbContextDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext)).ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using in-memory database with unique name per factory instance
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
            });

            // Replace authentication with test authentication that always succeeds
            services.AddSingleton<IPolicyEvaluator, FakePolicyEvaluator>();
        });

        // Use test environment - this will skip database migrations
        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Helper method to seed data for tests. Call this from your test's InitializeAsync.
    /// </summary>
    public async Task SeedDatabaseAsync(Action<ApplicationDbContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        // Seed data
        seedAction(context);
        
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Helper method to execute database operations in a scope
    /// </summary>
    public async Task<T> ExecuteDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    /// <summary>
    /// Helper method to execute database operations in a scope (void return)
    /// </summary>
    public async Task ExecuteDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(context);
    }
}

/// <summary>
/// Fake policy evaluator that bypasses all authorization for tests
/// </summary>
public class FakePolicyEvaluator : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "Test"));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy, AuthenticateResult authenticationResult, HttpContext context, object? resource)
    {
        return Task.FromResult(PolicyAuthorizationResult.Success());
    }
}
