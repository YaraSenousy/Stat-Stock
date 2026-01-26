using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StatStock.Infrastructure.Data;
using StatStock.Web.Api.Services;

namespace StatStock.Web.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
        ApplicationDbContext context,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generate JWT token for API access
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> GetToken([FromBody] TokenRequest request)
    {
        try
        {
            // Simple API key validation (in production, use proper authentication)
            // For demo purposes, we'll validate against a configured API key
            var validApiKey = _configuration["ApiKey"] ?? "demo-api-key-12345";
            
            if (request.ApiKey != validApiKey)
            {
                _logger.LogWarning("Invalid API key attempt from {Email}", request.Email);
                return Unauthorized(new { message = "Invalid API key" });
            }

            // Validate user exists (optional - for more secure implementation)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            string userId, email, role;
            
            if (user != null)
            {
                userId = user.Id;
                email = user.Email;
                role = user.Role.ToString();
            }
            else
            {
                // For B2B clients without user accounts, generate a generic token
                userId = $"api-client-{Guid.NewGuid()}";
                email = request.Email ?? "unknown@api.client";
                role = "B2BClient";
            }

            var token = _tokenService.GenerateToken(userId, email, role);

            _logger.LogInformation("Token generated for {Email}", email);

            return Ok(new TokenResponse
            {
                Token = token,
                ExpiresIn = int.Parse(_configuration["Jwt:ExpiryHours"] ?? "24") * 3600,
                TokenType = "Bearer"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating token");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Validate current JWT token
    /// </summary>
    /// <returns>Token validation result</returns>
    [HttpGet("validate")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(typeof(TokenValidationResponse), StatusCodes.Status200OK)]
    public ActionResult<TokenValidationResponse> ValidateToken()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Ok(new TokenValidationResponse
        {
            Valid = true,
            UserId = userId ?? string.Empty,
            Email = email ?? string.Empty,
            Role = role ?? string.Empty
        });
    }
}

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
