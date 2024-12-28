using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using EventDrivenDemo.Functions.Lib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace EventDrivenDemo.Functions
{
    public class LineExtractedConsumer
    {
        private readonly ILogger<LineExtractedConsumer> _logger;
        private readonly TableClient _tableClient;
        private readonly string _tableName;

        public LineExtractedConsumer(
            ILogger<LineExtractedConsumer> logger,
            TableServiceClient tableServiceClient,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tableName = configuration["TableName:FileLines"] 
                ?? throw new ArgumentNullException("TableName:FileLines configuration missing");
            _tableClient = tableServiceClient?.GetTableClient(_tableName) 
                ?? throw new ArgumentNullException(nameof(tableServiceClient));
        }

        [Function(nameof(LineExtractedConsumer))]
        public async Task Run(
            [ServiceBusTrigger("line-extracted", "file", Connection = "ServiceBusConnectionString")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            ArgumentNullException.ThrowIfNull(message);
            _logger.LogInformation("Message {id} picked up.", message.MessageId);

            try
            {
                var fullEvent = JsonConvert.DeserializeObject<DemoEvent<FileRow>>(
                    Encoding.UTF8.GetString(message.Body)) 
                    ?? throw new InvalidOperationException("Failed to deserialize message");
                
                ValidateEvent(fullEvent);
                var entity = CreateTableEntity(fullEvent);
                
                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Added line {line} from file {file} to table storage", 
                    fullEvent.Source, fullEvent.CorrelationId);
            }
            catch (JsonSerializationException ex)
            {
                _logger.LogError(ex, "Invalid message format for {id}", message.MessageId);
                await DeadLetterWithDetails(message, messageActions, "Invalid message format", ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation failed for {id}", message.MessageId);
                await DeadLetterWithDetails(message, messageActions, "Validation failed", ex);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing {id}", message.MessageId);
                await DeadLetterWithDetails(message, messageActions, "Unexpected error", ex);
                return;
            }

            await messageActions.CompleteMessageAsync(message);
        }

        private static TableEntity CreateTableEntity(DemoEvent<FileRow> fullEvent) =>
            new(
                partitionKey: fullEvent.Source.ToString(),
                rowKey: fullEvent.CorrelationId.ToString())
            {
                { "Name", fullEvent.EventData.Name },
                { "Type", fullEvent.EventData.Type },
                { "Location", fullEvent.EventData.Location },
                { "Notes", fullEvent.EventData.Notes },
                { "ProcessedDate", fullEvent.EventData.DateEntered }
            };

        private static void ValidateEvent(DemoEvent<FileRow> fullEvent)
        {
            ArgumentNullException.ThrowIfNull(fullEvent.EventData);
            ArgumentNullException.ThrowIfNull(fullEvent.Source);
            ArgumentNullException.ThrowIfNull(fullEvent.CorrelationId);
        }

        private static async Task DeadLetterWithDetails(
            ServiceBusReceivedMessage message, 
            ServiceBusMessageActions messageActions, 
            string reason, 
            Exception ex)
        {
            var properties = new Dictionary<string, object>
            {
                { "Error", reason },
                { "ErrorDetails", ex.ToString() }
            };
            await messageActions.DeadLetterMessageAsync(message, properties);
        }
    }
}
