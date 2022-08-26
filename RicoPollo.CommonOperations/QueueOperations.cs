using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace RicoPollo.CommonOperations
{
    public class QueueOperations
    {
        public static void SendQueueMessage(string queueName, string serializedMessage)
        {
            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();
            
            var connectionString = config.GetValue<string>("AzureWebJobsStorage");

            var queueClient = new QueueClient(connectionString, queueName, new QueueClientOptions() { MessageEncoding = QueueMessageEncoding.Base64});
            queueClient.CreateIfNotExists();

            if (queueClient.Exists())
            {
                queueClient.SendMessage(serializedMessage);
            }
        }
    }
}