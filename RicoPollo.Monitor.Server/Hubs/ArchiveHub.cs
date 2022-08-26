using Microsoft.AspNetCore.SignalR;
using RicoPollo.Models;

namespace RicoPollo.Monitor.Server.Hubs
{
    public class ArchiveHub : Hub
    {
        public async Task UpdateArchive(List<Ticket> tickets)
        {
            await Clients.All.SendAsync("ArchiveUpdated", tickets);
        }
    }
}
