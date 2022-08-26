using Newtonsoft.Json;
using RicoPollo.CommonOperations;
using RicoPollo.Models;

namespace RicoPollo.Cashier
{
    class Program
    {
        /// <summary>
        /// Rico Pollo Cashier CLI
        /// </summary>
        /// <param name="item">Which item to operate on: ticket, chicken, fries or soda</param>
        /// <param name="command">Which command to issue: create</param>
        /// <param name="count">How many repetitions of the command?</param>
        static int Main(string item = "ticket", string command = "issue", int count = 1)
        {
            switch((item, command, count))
            {
               case ("ticket", "issue", _):
                    for (int i = 0; i < count; i++) {
                        IssueTicket(i);
                        Thread.Sleep(500);
                    }
                    break;

               default: Console.WriteLine("Command Syntax Error");  return 0;
            }

            Console.WriteLine($"Running: {command} {count} {(count == 1 ? item : item+"s")}...");

            return 0;
        }

        static void IssueTicket(int order)
        {
            TicketUpsert newTicketMessage = new(Guid.NewGuid(), order, ItemTypes.Ticket, ItemOperations.Create);
            QueueOperations.SendQueueMessage("ticketwheel", JsonConvert.SerializeObject(newTicketMessage));
        }
    }
}