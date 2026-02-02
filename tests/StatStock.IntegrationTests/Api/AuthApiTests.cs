using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StatStock.Infrastructure.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all DbContext registrations
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext)).ToList();
                
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.UseInMemoryDatabase("AuthApiTestDb_" + Guid.NewGuid());
                });
            });
        });
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region POST /api/auth/token

    [Fact]
    public async Task GetToken_ShouldReturn200_WithValidCredentials()
    {
        // Arrange
        var request = new TokenRequest
        {
            Email = "test@example.com",
            ApiKey = "demo-api-key-12345"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse!.Token.Should().NotBeNullOrEmpty();
        tokenResponse.ExpiresIn.Should().BeGreaterThan(0);
        tokenResponse.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task GetToken_ShouldReturn401_WithInvalidApiKey()
    {
        // Arrange
        var request = new TokenRequest
        {
            Email = "test@example.com",
            ApiKey = "invalid-api-key"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorResponse!.Message.Should().Contain("Invalid API key");
    }

    [Fact]
    public async Task GetToken_ShouldGenerateValidJWT()
    {
        // Arrange
        var request = new TokenRequest
        {
            Email = "jwt-test@example.com",
            ApiKey = "demo-api-key-12345"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        // JWT should have 3 parts separated by dots
        var tokenParts = tokenResponse!.Token.Split('.');
        tokenParts.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetToken_ShouldAcceptDifferentEmails()
    {
        // Arrange
        var emails = new[] { "user1@test.com", "user2@test.com", "admin@test.com" };

        foreach (var email in emails)
        {
            var request = new TokenRequest
            {
                Email = email,
                ApiKey = "demo-api-key-12345"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            tokenResponse!.Token.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetToken_ShouldHandleEmptyEmail(string? email)
    {
        // Arrange
        var request = new TokenRequest
        {
            Email = email!,
            ApiKey = "demo-api-key-12345"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResponse!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToken_ShouldSetExpirationTime()
    {
        // Arrange
        var request = new TokenRequest
        {
            Email = "expiry-test@example.com",
            ApiKey = "demo-api-key-12345"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        // Default is 24 hours = 86400 seconds
        tokenResponse!.ExpiresIn.Should().Be(86400);
    }

    #endregion

    #region GET /api/auth/validate

    [Fact]
    public async Task ValidateToken_ShouldReturn200_WithValidToken()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auth/validate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var validationResponse = await response.Content.ReadFromJsonAsync<TokenValidationResponse>();
        validationResponse.Should().NotBeNull();
        validationResponse!.Valid.Should().BeTrue();
        validationResponse.UserId.Should().NotBeNullOrEmpty();
        validationResponse.Email.Should().NotBeNullOrEmpty();
        validationResponse.Role.Should().Be("B2BClient");
    }

    [Fact]
    public async Task ValidateToken_ShouldReturn401_WithoutToken()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/validate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidateToken_ShouldReturn401_WithInvalidToken()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token-string");

        // Act
        var response = await _client.GetAsync("/api/auth/validate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidateToken_ShouldReturnUserInfo()
    {
        // Arrange
        var email = "validation-test@example.com";
        var tokenRequest = new TokenRequest
        {
            Email = email,
            ApiKey = "demo-api-key-12345"
        };

        var tokenResponse = await _client.PostAsJsonAsync("/api/auth/token", tokenRequest);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
        
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auth/validate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var validationResponse = await response.Content.ReadFromJsonAsync<TokenValidationResponse>();
        validationResponse!.Valid.Should().BeTrue();
        validationResponse.UserId.Should().NotBeNullOrEmpty();
        validationResponse.Role.Should().Be("B2BClient");
    }

    [Fact]
    public async Task ValidateToken_ShouldWork_WithBearerPrefix()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auth/validate");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Token Usage Flow Tests

    [Fact]
    public async Task TokenFlow_ShouldAllowAccessToProtectedEndpoints()
    {
        // Arrange - Get token
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to access protected endpoint
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenFlow_ShouldDenyAccessWithoutToken()
    {
        // Act - Try to access protected endpoint without token
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenFlow_ShouldAllowMultipleRequests()
    {
        // Arrange
        var token = await GetValidTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Make multiple requests with same token
        var response1 = await _client.GetAsync("/api/products");
        var response2 = await _client.GetAsync("/api/products/categories");
        var response3 = await _client.GetAsync("/api/orders");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenFlow_ShouldGenerateUniqueTokensForDifferentRequests()
    {
        // Arrange & Act
        var token1 = await GetValidTokenAsync("user1@test.com");
        var token2 = await GetValidTokenAsync("user2@test.com");

        // Assert
        token1.Should().NotBe(token2);
    }

    #endregion

    #region Helper Methods

    private async Task<string> GetValidTokenAsync(string email = "test@example.com")
    {
        var request = new TokenRequest
        {
            Email = email,
            ApiKey = "demo-api-key-12345"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/token", request);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse!.Token;
    }

    #endregion
}

#region DTOs

public class TokenRequest
{
    public string Email { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}

public class TokenValidationResponse
{
    public bool Valid { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
}

#endregion
