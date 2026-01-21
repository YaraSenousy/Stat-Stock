using Microsoft.AspNetCore.SignalR;

namespace StatStock.Web.Hubs;

public class DashboardHub : Hub
{
    public async Task SendDashboardUpdate(string message)
    {
        await Clients.All.SendAsync("ReceiveDashboardUpdate", message);
    }

    public async Task SendStockAlert(string productName, int currentStock)
    {
        await Clients.All.SendAsync("ReceiveStockAlert", productName, currentStock);
    }

    public async Task SendOrderUpdate(string orderNumber, string status)
    {
        await Clients.All.SendAsync("ReceiveOrderUpdate", orderNumber, status);
    }
}
