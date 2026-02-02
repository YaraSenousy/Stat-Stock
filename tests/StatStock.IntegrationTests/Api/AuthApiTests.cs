using FluentAssertions;
using StatStock.Web.Api.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace StatStock.IntegrationTests.Api;

public class AuthApiTests : IntegrationTestBase
{
    public AuthApiTests(StatStockWebApplicationFactory factory) : base(factory)
    {
    }

    public override Task InitializeAsync()
    {
        // Don't call base - we need to test auth without being authenticated
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetToken_ShouldReturn200_WithValidCredentials()
    {
        // Arrange
        var request = new
        {
            email = "admin@statstock.com",
            apiKey = "demo-api-key-12345"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToken_ShouldReturn401_WithInvalidEmail()
    {
        // Arrange
        var request = new
        {
            email = "invalid@example.com",
            apiKey = "demo-api-key-12345"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetToken_ShouldReturn401_WithInvalidApiKey()
    {
        // Arrange
        var request = new
        {
            email = "admin@statstock.com",
            apiKey = "invalid-key"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetToken_ShouldReturn400_WithMissingEmail()
    {
        // Arrange
        var request = new
        {
            apiKey = "demo-api-key-12345"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetToken_ShouldReturn400_WithMissingApiKey()
    {
        // Arrange
        var request = new
        {
            email = "admin@statstock.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ShouldReturn401_WithoutToken()
    {
        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ShouldReturn401_WithInvalidToken()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_ShouldReturn200_WithValidToken()
    {
        // Arrange
        var tokenResponse = await Client.PostAsJsonAsync("/api/auth/token", new
        {
            email = "admin@statstock.com",
            apiKey = "demo-api-key-12345"
        });
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>();
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult!.Data.Token);

        // Act
        var response = await Client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefreshToken_ShouldReturn200_WithValidToken()
    {
        // Arrange
        var tokenResponse = await Client.PostAsJsonAsync("/api/auth/token", new
        {
            email = "admin@statstock.com",
            apiKey = "demo-api-key-12345"
        });
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>();
        var refreshRequest = new
        {
            token = tokenResult!.Data.Token
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>();
        result!.Data.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RevokeToken_ShouldReturn200_WithValidToken()
    {
        // Arrange
        var tokenResponse = await Client.PostAsJsonAsync("/api/auth/token", new
        {
            email = "admin@statstock.com",
            apiKey = "demo-api-key-12345"
        });
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<ApiResponse<TokenResponse>>();
        var revokeRequest = new
        {
            token = tokenResult!.Data.Token
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/auth/revoke", revokeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
