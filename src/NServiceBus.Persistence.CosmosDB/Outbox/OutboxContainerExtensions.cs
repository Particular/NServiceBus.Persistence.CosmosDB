namespace NServiceBus.Persistence.CosmosDB;

using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

static class OutboxContainerExtensions
{
    public static async Task<OutboxRecord> ReadOutboxRecord(this Container container, string messageId, PartitionKey partitionKey, JsonSerializer serializer, CancellationToken cancellationToken = default)
    {
        ResponseMessage responseMessage = await container.ReadItemStreamAsync(messageId, partitionKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        _ = responseMessage.EnsureSuccessStatusCode();

        using var streamReader = new StreamReader(responseMessage.Content);
        using var jsonReader = new JsonTextReader(streamReader);

        return serializer.Deserialize<OutboxRecord>(jsonReader);
    }
}