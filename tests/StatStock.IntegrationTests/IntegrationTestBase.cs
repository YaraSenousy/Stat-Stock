using Microsoft.Extensions.DependencyInjection;
using StatStock.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests;

/// <summary>
/// Base class for integration tests with common setup and helpers
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<StatStockWebApplicationFactory>, IAsyncLifetime
{
    protected readonly StatStockWebApplicationFactory Factory;
    protected HttpClient Client = null!;
    protected string AuthToken = string.Empty;

    protected IntegrationTestBase(StatStockWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public virtual async Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        // Don't authenticate by default - tests can override if needed
    }

    public virtual Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get authentication token for API calls
    /// </summary>
    protected async Task<string> GetAuthTokenAsync()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new
        {
            email = "admin@statstock.com",
            apiKey = "demo-api-key-12345"
        });

        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return result?.Token ?? string.Empty;
    }

    /// <summary>
    /// Execute database operations in a new scope
    /// </summary>
    protected async Task ExecuteDbAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Execute database operations in a new scope with return value
    /// </summary>
    protected async Task<T> ExecuteDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    private class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
