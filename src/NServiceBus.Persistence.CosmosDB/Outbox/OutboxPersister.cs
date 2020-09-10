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
            var container = context.Get<Container>();
            var cosmosOutboxTransaction = new CosmosOutboxTransaction(container);

            if (context.TryGet<PartitionKey>(out var partitionKey))
            {
                cosmosOutboxTransaction.PartitionKey = partitionKey;
            }

            return Task.FromResult((OutboxTransaction)cosmosOutboxTransaction);
        }

        public async Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            if (!context.TryGet<PartitionKey>(out var partitionKey) || !context.TryGet<Container>(out var container))
            {
                // we return null here to enable outbox work at logical stage
                return null;
            }

            var outboxRecord = await container.ReadOutboxRecord(messageId, partitionKey, serializer, context)
                .ConfigureAwait(false);

            return outboxRecord != null ? new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations) : null;
        }

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
            var cosmosTransaction = (CosmosOutboxTransaction)transaction;

            if (cosmosTransaction == null || cosmosTransaction.SuppressStoreAndCommit || cosmosTransaction.PartitionKey == null)
            {
                return Task.CompletedTask;
            }

            var partitionKeyPath = context.Get<PartitionKeyPath>();

            cosmosTransaction.StorageSession.AddOperation(new OutboxStore(new OutboxRecord
                {
                    Id = message.MessageId,
                    TransportOperations = message.TransportOperations
                },
                cosmosTransaction.PartitionKey.Value,
                partitionKeyPath,
                context));
            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context)
        {
            var partitionKey = context.Get<PartitionKey>();

            var outboxRecord = new OutboxRecord
            {
                Id = messageId,
                Dispatched = true
            };

            var createJObject = JObject.FromObject(outboxRecord);
            createJObject.Add("id", outboxRecord.Id);

            // TODO: Make TTL configurable
            createJObject.Add("ttl", 100);

            createJObject.EnrichWithPartitionKeyIfNecessary(partitionKey.ToString(), partitionKeyPath);

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
