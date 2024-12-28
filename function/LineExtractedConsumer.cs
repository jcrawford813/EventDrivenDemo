using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using EventDrivenDemo.Functions.Lib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Data.Tables;

namespace EventDrivenDemo.Functions
{
    public class LineExtractedConsumer(
        ILogger<LineExtractedConsumer> logger,
        TableServiceClient tableServiceClient)
    {
        private readonly ILogger<LineExtractedConsumer> _logger = logger;
        private readonly TableClient _tableClient = tableServiceClient.GetTableClient("FileLines");

        [Function(nameof(LineExtractedConsumer))]
        public async Task Run(
            [ServiceBusTrigger("line-extracted", "file", Connection = "ServiceBusConnectionString")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("Message {id} picked up.", message.MessageId);

            try
            {
                var fullEvent = JsonConvert.DeserializeObject<DemoEvent<FileRow>>(Encoding.UTF8.GetString(message.Body));
                
                var entity = new TableEntity(
                    partitionKey: fullEvent.Source.ToString(),
                    rowKey: fullEvent.CorrelationId.ToString())
                {
                    { "Name", fullEvent.EventData.Name },
                    { "Type", fullEvent.EventData.Type },
                    { "Location", fullEvent.EventData.Location },
                    { "Notes", fullEvent.EventData.Notes },
                    { "ProcessedDate", fullEvent.EventData.DateEntered }
                };

                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Added line {line} from file {file} to table storage", 
                    fullEvent.Source, fullEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing message {id}; Error: {error}", message.MessageId, ex.Message);
                await messageActions.DeadLetterMessageAsync(message);
                return;
            }

            await messageActions.CompleteMessageAsync(message);
        }
    }
}
