using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RicoPollo.CommonOperations;
using RicoPollo.Models;

namespace ChickenOven
{
    public class Bake
    {
        public static void ProcessBakeRequest([QueueTrigger("chickenoven")] QueueMessage queueMessage, ILogger logger)
        {
            BakeChicken? bakeChickenMessage = JsonConvert.DeserializeObject<BakeChicken>(queueMessage.Body.ToString());

            if (bakeChickenMessage is not null)
            {
                Console.WriteLine($"RECEIVED: Bake Chicken for {bakeChickenMessage.TicketId}");

                var random = new Random();
                ItemOperations itemOperation = random.Next(0, 10) > 5 ? ItemOperations.MarkCompleted : ItemOperations.MarkIncompleted;

                TicketUpsert tickettItemUpdateMessage = new(bakeChickenMessage.TicketId, bakeChickenMessage.TicketOrder, ItemTypes.Chicken, itemOperation);
                
                Thread.Sleep(random.Next(5, 9) * 1000);

                QueueOperations.SendQueueMessage("ticketwheel", JsonConvert.SerializeObject(tickettItemUpdateMessage));

                Console.WriteLine($"SEND: Bake Chicken for {bakeChickenMessage.TicketId} is READY");
            }
        }
    }
}
