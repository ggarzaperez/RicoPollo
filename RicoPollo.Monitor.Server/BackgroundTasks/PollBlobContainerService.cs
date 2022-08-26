using Azure.Storage.Blobs;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using RicoPollo.Models;
using RicoPollo.Monitor.Server.Hubs;

namespace RicoPollo.Monitor.Server.BackgroundTasks
{
    public class PollBlobContainerService : IHostedService, IDisposable
    {
        private readonly ILogger<PollBlobContainerService> _logger;
        private Timer? _timer = null;
        private BlobServiceClient blobServiceClient;
        private BlobContainerClient blobContainerClient;
        private BlobContainerClient archiveContainerClient;
        private IHubContext<TicketsHub> ticketsHub;
        private IHubContext<ArchiveHub> archiveHub;

        public PollBlobContainerService(IHubContext<TicketsHub> _ticketsHub, IHubContext<ArchiveHub> _archiveHub, IConfiguration configuration, ILogger<PollBlobContainerService> logger)
        {
            ticketsHub = _ticketsHub;
            archiveHub = _archiveHub;

            var connectionString = configuration.GetValue<string>("AzureWebJobsStorage");
            blobServiceClient = new BlobServiceClient(connectionString);

            blobContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("PolledContainerName"));
            archiveContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("PolledArchiveName"));

            _logger = logger;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blob Polling Service Running...");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            var tickets = new List<Ticket>();
            var archivedTickets = new List<Ticket>();

            if (blobContainerClient.Exists())
            {

                foreach (var blobItem in blobContainerClient.GetBlobs())
                {
                    var blobClient = blobContainerClient.GetBlobClient(blobItem.Name);

                    if (blobClient.Exists())
                    {
                        try
                        {
                            var blobDownloadResult = blobClient.DownloadContent();

                            Ticket? ticket = JsonConvert.DeserializeObject<Ticket>(blobDownloadResult.Value.Content.ToString());

                            if (ticket is not null)
                            {
                                tickets.Add(ticket);
                            }
                        }
                        catch (Exception)
                        {
                            //Do nothing, the read is a best effort approach. If the blob is not there anymore
                            //We just ignore it for the next rendering cycle.
                        }
                    }
                }
            }

            if (archiveContainerClient.Exists())
            {

                foreach (var blobItem in archiveContainerClient.GetBlobs())
                {
                    var blobClient = archiveContainerClient.GetBlobClient(blobItem.Name);

                    if (blobClient.Exists())
                    {
                        try
                        {
                            var blobDownloadResult = blobClient.DownloadContent();

                            Ticket? ticket = JsonConvert.DeserializeObject<Ticket>(blobDownloadResult.Value.Content.ToString());

                            if (ticket is not null)
                            {
                                archivedTickets.Add(ticket);
                            }
                        }
                        catch (Exception)
                        {
                            //Do nothing, the read is a best effort approach. If the blob is not there anymore
                            //We just ignore it for the next rendering cycle.
                        }
                    }
                }
            }

            ticketsHub.Clients.All.SendAsync("TicketsUpdated", tickets).Wait();
            archiveHub.Clients.All.SendAsync("ArchiveUpdated", archivedTickets).Wait();

            _logger.LogInformation("Blob Polling Service is done polling");
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blob Polling Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
