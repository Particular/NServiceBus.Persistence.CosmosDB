namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Extensibility;
    using NServiceBus.Outbox;

    class OutboxPersister : IOutboxStorage
    {
        JsonSerializer serializer;

        public OutboxPersister(JsonSerializerSettings jsonSerializerSettings)
        {
            serializer = JsonSerializer.Create(jsonSerializerSettings);
        }

        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            return Task.FromResult((OutboxTransaction)new CosmosOutboxTransaction());
        }

        public async Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            // backdoor to enable the outbox tests to pass but in theory someone could add it if they want
            if (!context.TryGet<PartitionKey>(out var partitionKey) || !context.TryGet<Container>(out var container))
            {
                // we should always return null to make the outbox "hack" work
                return null;
            }

            // this path is really only ever reached during testing
            var outboxRecord = await container.ReadOutboxRecord(messageId, partitionKey, serializer, context)
                .ConfigureAwait(false);

            return outboxRecord != null ? new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations) : null;
        }

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
            var cosmosTransaction = transaction as CosmosOutboxTransaction;

            if (cosmosTransaction == null)
            {
                return Task.CompletedTask;
            }

            // backdoor to enable the outbox tests to pass but in theory someone could add it if they want
            if (context.TryGet<PartitionKey>(out var partitionKey) &&
                context.TryGet<Container>(out var container) &&
                context.TryGet<string>(ContextBagKeys.PartitionKeyPath, out var partitionKeyPath))
            {
                cosmosTransaction.StorageSession = new StorageSession(container, partitionKey, partitionKeyPath, false);
            }

            cosmosTransaction.StorageSession?.Modifications.Add(new OutboxStore(new OutboxRecord
                {
                    Id = message.MessageId,
                    TransportOperations = message.TransportOperations
                },
                context));
            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context)
        {
            var partitionKey = context.Get<PartitionKey>();
            var partitionKeyPath = context.Get<string>(ContextBagKeys.PartitionKeyPath);
            var container = context.Get<Container>();

            //TODO: we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = partitionKeyPath.Replace("/", ".");
            var segments = pathToMatch.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if (i == segments.Length - 1)
                {
                    current[segmentName] = JArray.Parse(partitionKey.ToString())[0];
                    continue;
                }

                current[segmentName] = new JObject();
                current = current[segmentName] as JObject;
            }

            var outboxRecord = new OutboxRecord
            {
                Id = messageId,
                Dispatched = true
            };

            var createJObject = JObject.FromObject(outboxRecord);
            createJObject.Add("id", outboxRecord.Id);

            // TODO: Make TTL configurable
            createJObject.Add("ttl", 100);

            createJObject.Merge(start);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(createJObject))))
            {
                var result = await container.UpsertItemStreamAsync(stream, partitionKey).ConfigureAwait(false);

                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new Exception($"Unable to update the outbox record: {result.ErrorMessage}");
                }
            }
        }

        internal static readonly string SchemaVersion = "1.0.0";
    }
}
