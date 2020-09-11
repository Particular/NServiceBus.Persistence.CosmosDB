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

        public OutboxPersister(ContainerHolder containerHolder, JsonSerializer serializer)
        {
            container = containerHolder.Container;
            partitionKeyPath = containerHolder.PartitionKeyPath;
            this.serializer = serializer;
        }

        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            var cosmosOutboxTransaction = new CosmosOutboxTransaction(container);

            if (context.TryGet<PartitionKey>(out var partitionKey))
            {
                cosmosOutboxTransaction.PartitionKey = partitionKey;
            }

            return Task.FromResult((OutboxTransaction)cosmosOutboxTransaction);
        }

        public async Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            if (!context.TryGet<PartitionKey>(out var partitionKey))
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
                serializer,
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

            var createJObject = JObject.FromObject(outboxRecord, serializer);

            // TODO: Make TTL configurable
            createJObject.Add("ttl", 100);

            createJObject.EnrichWithPartitionKeyIfNecessary(partitionKey.ToString(), partitionKeyPath);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(createJObject.ToString(Formatting.None))))
            {
                var result = await container.UpsertItemStreamAsync(stream, partitionKey).ConfigureAwait(false);

                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new Exception($"Unable to update the outbox record: {result.ErrorMessage}");
                }
            }
        }

        internal static readonly string SchemaVersion = "1.0.0";
        readonly JsonSerializer serializer;
        readonly Container container;
        readonly string partitionKeyPath;
    }
}
