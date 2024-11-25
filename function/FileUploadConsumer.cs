using System.IO;
using System.Threading.Tasks;
using EventDrivenDemo.Functions.Lib;
using System.Globalization;
using CsvHelper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace EventDrivenDemo.Functions
{
    public class FileUploadConsumer: FunctionBase
    {
        private readonly ILogger<FileUploadConsumer> _logger;

        public FileUploadConsumer(ILogger<FileUploadConsumer> logger, IConfigurationRoot configuration)
            : base(configuration)
        {
            _logger = logger;
        }

        [Function(nameof(FileUploadConsumer))]
        public async Task Run([BlobTrigger("demo-files/{name}", Connection = "StorageConnectionString")] Stream stream, string name)
        {
            _logger.LogInformation("Picked up file from upload folder.");
            var fileId = Guid.NewGuid();
            var rowId = 0;

            using var blobStreamReader = new StreamReader(stream);
            var csvReader = new CsvReader(blobStreamReader, CultureInfo.InvariantCulture);

            var data = csvReader.GetRecords<FileRow>();

            foreach (var row in data)
            {
                var currentRow = ++rowId;

                //Format into a standard event, and send to the target topic.
                var eventMessage = new DemoEvent<FileRow>()
                {
                    EventName = "LineExtracted",
                    EventDate = DateTime.Now,
                    CorrelationId = currentRow,
                    Source = fileId,
                    EventData = row
                };

                var sbMessage = new Message(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventMessage)));

                try
                {
                    var client = new TopicClient(_configuration.GetValue<string>("ServiceBusConnectionString"), "line-extracted");
                    await client.SendAsync(sbMessage);

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while processing row { currentRow }: { ex.Message } \n {ex.StackTrace}");
                }
            }

            //Write Correlation Data

            _logger.LogInformation($"File Upload Consumer Processed blob\n Name: {name}");
        }
    }
}
