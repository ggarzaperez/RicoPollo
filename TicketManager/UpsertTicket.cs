using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RicoPollo.CommonOperations;
using RicoPollo.Models;
using System.Text;

namespace TicketManager
{
    public class UpsertTicket
    {
        public static void ProcessTicketUpsertRequest([QueueTrigger("ticketwheel")] QueueMessage queueMessage, ILogger logger)
        {       
            TicketUpsert? ticketUpsertMessage = JsonConvert.DeserializeObject<TicketUpsert>(queueMessage.Body.ToString());

            if (ticketUpsertMessage is not null)
            {
                Console.WriteLine($"RECEIVED: Ticket Upsert for {ticketUpsertMessage.TicketId} to {Enum.GetName(ticketUpsertMessage.ItemOperation)} the {Enum.GetName(ticketUpsertMessage.ItemType)}");

                switch (ticketUpsertMessage.ItemOperation)
                {
                    case ItemOperations.Create: CreateTicket(ticketUpsertMessage.TicketId, ticketUpsertMessage.TicketOrder); break;
                    case ItemOperations.MarkCompleted: MarkItemCompleted(ticketUpsertMessage.TicketId, ticketUpsertMessage.ItemType); break;
                    case ItemOperations.MarkIncompleted: MarkItemIncompleted(ticketUpsertMessage.TicketId, ticketUpsertMessage.ItemType); break;
                }
            }
        }

        public static void CreateTicket(Guid ticketId, int ticketOrder)
        {
            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();
            var connectionString = config.GetValue<string>("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var ticketsContainer = blobServiceClient.GetBlobContainerClient("ticketwheel");
            ticketsContainer.CreateIfNotExists();

            Ticket newTicket = new(ticketId, ticketOrder, ItemStatuses.Pending, 0, ItemStatuses.Pending, 0, ItemStatuses.Pending, 0);
            var serializedTicket = JsonConvert.SerializeObject(newTicket);
            var ticketStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedTicket));

            ticketsContainer.UploadBlob(ticketId.ToString(), ticketStream);

            QueueOperations.SendQueueMessage("chickenoven", JsonConvert.SerializeObject(new BakeChicken(ticketId, ticketOrder)));
            QueueOperations.SendQueueMessage("airfryer", JsonConvert.SerializeObject(new FryFrozenFries(ticketId, ticketOrder)));
        }

        private static void MarkItemCompleted(Guid ticketId, ItemTypes itemType)
        {
            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();
            var connectionString = config.GetValue<string>("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var ticketsContainer = blobServiceClient.GetBlobContainerClient("ticketwheel");
            ticketsContainer.CreateIfNotExists();
            var ticketBlobClient = ticketsContainer.GetBlobClient(ticketId.ToString());

            if (ticketBlobClient.Exists())
            {
                var ticketLeaseClient = ticketBlobClient.GetBlobLeaseClient();
                string ticketLeaseId = "";

                while (string.IsNullOrEmpty(ticketLeaseId))                
                {
                    try
                    {
                        ticketLeaseId = ticketLeaseClient.Acquire(TimeSpan.FromSeconds(15)).Value.LeaseId;
                    }
                    catch (RequestFailedException ex)
                    {
                        if (ex.Status != 409)
                            throw;
                    }

                    Thread.Sleep(500);
                }

                var blobDownloadResult = ticketBlobClient.DownloadContent();

                Ticket? currentTicket = JsonConvert.DeserializeObject<Ticket>(blobDownloadResult.Value.Content.ToString());

                if (currentTicket is not null)
                {
                    var updatedTicket = itemType switch
                    {
                        ItemTypes.Chicken => currentTicket with { ChickenStatus = ItemStatuses.Complete },
                        ItemTypes.Fries => currentTicket with { FriesStatus = ItemStatuses.Complete },
                        ItemTypes.TakeoutBag => currentTicket with { TakeoutStatus = ItemStatuses.Complete},
                        _ => throw new NotImplementedException()
                    };

                    if (updatedTicket.TakeoutStatus == ItemStatuses.Complete) 
                    {
                        ticketBlobClient.Delete(DeleteSnapshotsOption.None, new BlobRequestConditions()
                        {
                            LeaseId = ticketLeaseId                            
                        });

                        var archiveContainer = blobServiceClient.GetBlobContainerClient("ticketarchive");
                        archiveContainer.CreateIfNotExists();

                        var serializedArchived = JsonConvert.SerializeObject(updatedTicket);
                        var archivedStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedArchived));

                        archiveContainer.UploadBlob(updatedTicket.TicketId.ToString(), archivedStream);

                        Console.WriteLine($"ARCHIVED: Ticket for {updatedTicket.TicketId}, as Takeout is Completed");
                        return;
                    }

                    var serializedTicket = JsonConvert.SerializeObject(updatedTicket);
                    var ticketStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedTicket));

                    BlobUploadOptions ticketUploadOptions = new BlobUploadOptions()
                    {
                        Conditions = new BlobRequestConditions()
                        {
                            LeaseId = ticketLeaseId,
                        }
                    };
                    ticketBlobClient.Upload(ticketStream, ticketUploadOptions);

                    Console.WriteLine($"STORED: Updated Ticket for {updatedTicket.TicketId} with a Completed {Enum.GetName(itemType)}");

                    if (updatedTicket.ChickenStatus == ItemStatuses.Complete && 
                        updatedTicket.FriesStatus == ItemStatuses.Complete &&
                        updatedTicket.TakeoutStatus == ItemStatuses.Pending)
                    {
                        Console.WriteLine($"SEND: Pack Takeout for {updatedTicket.TicketId}");
                        QueueOperations.SendQueueMessage("takeouttray", JsonConvert.SerializeObject(new PackTakeout(ticketId, updatedTicket.TicketOrder)));
                    }
                }

                ticketLeaseClient.Release();
            }
        }

        private static void MarkItemIncompleted(Guid ticketId, ItemTypes itemType)
        {
            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();
            var connectionString = config.GetValue<string>("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var ticketsContainer = blobServiceClient.GetBlobContainerClient("ticketwheel");
            ticketsContainer.CreateIfNotExists();
            var ticketBlobClient = ticketsContainer.GetBlobClient(ticketId.ToString());

            if (ticketBlobClient.Exists())
            {
                var ticketLeaseClient = ticketBlobClient.GetBlobLeaseClient();
                string ticketLeaseId = "";

                while (string.IsNullOrEmpty(ticketLeaseId))
                {
                    try
                    {
                        ticketLeaseId = ticketLeaseClient.Acquire(TimeSpan.FromSeconds(15)).Value.LeaseId;
                    }
                    catch (RequestFailedException ex)
                    {
                        if (ex.Status != 409)
                            throw;
                    }

                    Thread.Sleep(500);
                }

                var blobDownloadResult = ticketBlobClient.DownloadContent();

                Ticket? currentTicket = JsonConvert.DeserializeObject<Ticket>(blobDownloadResult.Value.Content.ToString());

                if (currentTicket is not null)
                {
                    if ((currentTicket.ChickenStatus == ItemStatuses.Failed && currentTicket.ChickenAttempt >= 3) ||
                        (currentTicket.FriesStatus == ItemStatuses.Failed && currentTicket.FriesAttempt >= 3) ||
                        (currentTicket.TakeoutStatus == ItemStatuses.Failed && currentTicket.TakeoutAttempt >= 3))
                    {
                        //Give up, retry policy has failed
                        //TODO: All pending operations should cancel
                        return;
                    }

                    var updatedTicket = itemType switch
                    {
                        ItemTypes.Chicken => currentTicket with { ChickenStatus = ItemStatuses.Failed, ChickenAttempt = currentTicket.ChickenAttempt + 1 },
                        ItemTypes.Fries => currentTicket with { FriesStatus = ItemStatuses.Failed, FriesAttempt = currentTicket.FriesAttempt + 1 },
                        ItemTypes.TakeoutBag => currentTicket with { TakeoutStatus = ItemStatuses.Failed, TakeoutAttempt = currentTicket.TakeoutAttempt + 1 },
                        _ => throw new NotImplementedException()
                    };

                    var serializedTicket = JsonConvert.SerializeObject(updatedTicket);
                    var ticketStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedTicket));

                    BlobUploadOptions ticketUploadOptions = new BlobUploadOptions()
                    {
                        Conditions = new BlobRequestConditions()
                        {
                            LeaseId = ticketLeaseId,
                        }
                    };
                    ticketBlobClient.Upload(ticketStream, ticketUploadOptions);

                    Console.WriteLine($"STORED: Updated Ticket for {updatedTicket.TicketId} with a Failed {Enum.GetName(itemType)}");

                    switch(itemType)
                    {
                        case ItemTypes.Chicken:
                            Console.WriteLine($"REATTEMPT SEND: Bake Chicken for {updatedTicket.TicketId} attempt {updatedTicket.ChickenAttempt}");
                            QueueOperations.SendQueueMessage("chickenoven", JsonConvert.SerializeObject(new BakeChicken(ticketId, updatedTicket.TicketOrder, updatedTicket.ChickenAttempt)));
                            break;
                        case ItemTypes.Fries:
                            Console.WriteLine($"REATTEMPT SEND: Fry Frozen Fries for {updatedTicket.TicketId} attempt {updatedTicket.FriesAttempt}");
                            QueueOperations.SendQueueMessage("airfryer", JsonConvert.SerializeObject(new FryFrozenFries(ticketId, updatedTicket.TicketOrder, updatedTicket.FriesAttempt)));
                            break;
                        case ItemTypes.TakeoutBag:
                            Console.WriteLine($"REATTEMPT SEND: Pack Takeout for {updatedTicket.TicketId} attempt {updatedTicket.TakeoutAttempt}");
                            QueueOperations.SendQueueMessage("takeouttray", JsonConvert.SerializeObject(new PackTakeout(ticketId, updatedTicket.TicketOrder, updatedTicket.TakeoutAttempt)));
                            break;
                    }
                }

                ticketLeaseClient.Release();
            }
        }

    }
}
