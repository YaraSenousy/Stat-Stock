using StatStock.Domain.Enums;
using StatStock.Web.Api.DTOs;
using System.Text;
using System.Text.Json;

namespace StatStock.Web.Api.Services;

public interface IWebhookService
{
    Task NotifyOrderCreated(OrderDto order);
    Task NotifyOrderStatusChanged(OrderDto order, OrderStatus oldStatus, OrderStatus newStatus);
}

public class WebhookService : IWebhookService
{
    private readonly ILogger<WebhookService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public WebhookService(
        ILogger<WebhookService> logger, 
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task NotifyOrderCreated(OrderDto order)
    {
        var webhookUrl = _configuration["Webhooks:OrderCreatedUrl"];
        
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogDebug("No webhook URL configured for order created events");
            return;
        }

        var payload = new
        {
            eventType = "order.created",
            timestamp = DateTime.UtcNow,
            data = order
        };

        await SendWebhookAsync(webhookUrl, payload);
    }

    public async Task NotifyOrderStatusChanged(OrderDto order, OrderStatus oldStatus, OrderStatus newStatus)
    {
        var webhookUrl = _configuration["Webhooks:OrderStatusChangedUrl"];
        
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogDebug("No webhook URL configured for order status changed events");
            return;
        }

        var payload = new
        {
            eventType = "order.status_changed",
            timestamp = DateTime.UtcNow,
            data = new
            {
                order = order,
                oldStatus = oldStatus.ToString(),
                newStatus = newStatus.ToString()
            }
        };

        await SendWebhookAsync(webhookUrl, payload);
    }

    private async Task SendWebhookAsync(string url, object payload)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook sent successfully to {Url}", url);
            }
            else
            {
                _logger.LogWarning("Webhook failed with status {StatusCode} for URL {Url}", response.StatusCode, url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending webhook to {Url}", url);
        }
    }
}
