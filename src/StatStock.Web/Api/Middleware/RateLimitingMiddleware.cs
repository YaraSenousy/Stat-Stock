using System.Collections.Concurrent;
using System.Net;

namespace StatStock.Web.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, ClientRateLimit> _clients = new();
    private readonly int _requestLimit;
    private readonly TimeSpan _timeWindow;

    public RateLimitingMiddleware(
        RequestDelegate next, 
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _requestLimit = int.Parse(configuration["RateLimiting:RequestLimit"] ?? "100");
        _timeWindow = TimeSpan.FromMinutes(int.Parse(configuration["RateLimiting:TimeWindowMinutes"] ?? "1"));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply rate limiting to API endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var clientRateLimit = _clients.GetOrAdd(clientId, _ => new ClientRateLimit());

        bool exceedsLimit = false;
        int remainingRequests = 0;

        lock (clientRateLimit)
        {
            var now = DateTime.UtcNow;

            // Clean up old requests outside the time window
            clientRateLimit.RequestTimestamps.RemoveAll(t => now - t > _timeWindow);

            // Check if client has exceeded rate limit
            if (clientRateLimit.RequestTimestamps.Count >= _requestLimit)
            {
                exceedsLimit = true;
            }
            else
            {
                // Add current request timestamp
                clientRateLimit.RequestTimestamps.Add(now);
                remainingRequests = _requestLimit - clientRateLimit.RequestTimestamps.Count;
            }
        }

        if (exceedsLimit)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
            
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            
            var response = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Rate limit exceeded",
                message = $"Maximum {_requestLimit} requests per {_timeWindow.TotalMinutes} minute(s) allowed",
                retryAfter = _timeWindow.TotalSeconds
            });

            await context.Response.WriteAsync(response);
            return;
        }

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _requestLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remainingRequests.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ((long)(DateTime.UtcNow.Add(_timeWindow) - DateTime.UnixEpoch).TotalSeconds).ToString();

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID from JWT token
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private class ClientRateLimit
    {
        public List<DateTime> RequestTimestamps { get; } = new();
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
