using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RicoPollo.CommonOperations;
using RicoPollo.Models;

namespace RicoPollo.TakeoutPacker
{
    public class Pack
    {
        public static void ProcessPackRequest([QueueTrigger("takeouttray")] QueueMessage queueMessage, ILogger logger)
        {
            PackTakeout? packTakeoutMessage = JsonConvert.DeserializeObject<PackTakeout>(queueMessage.Body.ToString());

            if (packTakeoutMessage is not null)
            {
                Console.WriteLine($"RECEIVED: Pack Takeout for {packTakeoutMessage.TicketId}");

                var random = new Random();
                ItemOperations itemOperation = random.Next(0, 10) > 4 ? ItemOperations.MarkCompleted : ItemOperations.MarkIncompleted;

                TicketUpsert tickettItemUpdateMessage = new(packTakeoutMessage.TicketId, packTakeoutMessage.TicketOrder, ItemTypes.TakeoutBag,itemOperation);

                Thread.Sleep(random.Next(3, 4) * 1000);

                QueueOperations.SendQueueMessage("ticketwheel", JsonConvert.SerializeObject(tickettItemUpdateMessage));

                Console.WriteLine($"SEND: Takeout packaging {packTakeoutMessage.TicketId} are READY");
            }
        }
    }
}
