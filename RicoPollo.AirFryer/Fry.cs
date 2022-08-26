using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RicoPollo.CommonOperations;
using RicoPollo.Models;

namespace RicoPollo.AirFryer
{
    public class Fry
    {
        public static void ProcessFryRequest([QueueTrigger("airfryer")] QueueMessage queueMessage, ILogger logger)
        {
            FryFrozenFries? fryFrozenFriesMessage = JsonConvert.DeserializeObject<FryFrozenFries>(queueMessage.Body.ToString());

            if (fryFrozenFriesMessage is not null)
            {
                Console.WriteLine($"RECEIVED: Fry frozen fries for {fryFrozenFriesMessage.TicketId}");

                var random = new Random();
                ItemOperations itemOperation = random.Next(0, 10) > 2 ? ItemOperations.MarkCompleted : ItemOperations.MarkIncompleted;

                TicketUpsert tickettItemUpdateMessage = new(fryFrozenFriesMessage.TicketId, fryFrozenFriesMessage.TicketOrder, ItemTypes.Fries,itemOperation);

                Thread.Sleep(random.Next(2, 5) * 1000);

                QueueOperations.SendQueueMessage("ticketwheel", JsonConvert.SerializeObject(tickettItemUpdateMessage));

                Console.WriteLine($"SEND: Fries for {fryFrozenFriesMessage.TicketId} are READY");
            }
        }
    }
}
