namespace NServiceBus.Persistence.CosmosDB
{
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    static class OutboxContainerExtensions
    {
        public static async Task<OutboxRecord> ReadOutboxRecord(this Container container, string messageId, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
        {
            var responseMessage = await container.ReadItemStreamAsync(messageId, partitionKey)
                .ConfigureAwait(false);

            if (responseMessage.StatusCode == HttpStatusCode.NotFound || responseMessage.Content == null)
            {
                return default;
            }

            using (var streamReader = new StreamReader(responseMessage.Content))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var outboxRecord = serializer.Deserialize<OutboxRecord>(jsonReader);

                    context.Set($"cosmos_etag:{messageId}", responseMessage.Headers.ETag);

                    return outboxRecord;
                }
            }
        }
    }
}