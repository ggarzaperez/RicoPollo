using Microsoft.AspNetCore.SignalR;
using RicoPollo.Models;

namespace RicoPollo.Monitor.Server.Hubs
{
    public class TicketsHub : Hub
    {
        public async Task UpdateTickets(List<Ticket> tickets)
        {
            await Clients.All.SendAsync("TicketsUpdated", tickets);
        }
    }
}
