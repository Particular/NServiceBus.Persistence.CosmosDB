namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Outbox;

    class OutboxPersister : IOutboxStorage
    {
        public OutboxPersister(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, int ttlInSeconds)
        {
            this.containerHolderResolver = containerHolderResolver;
            this.serializer = serializer;
            this.ttlInSeconds = ttlInSeconds;
        }

        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

            if (context.TryGet<PartitionKey>(out var partitionKey))
            {
                cosmosOutboxTransaction.PartitionKey = partitionKey;
            }

            return Task.FromResult((OutboxTransaction)cosmosOutboxTransaction);
        }

        public async Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            var setAsDispatchedHolder = new SetAsDispatchedHolder
            {
                ContainerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context)
            };
            context.Set(setAsDispatchedHolder);

            if (!context.TryGet<PartitionKey>(out var partitionKey))
            {
                // we return null here to enable outbox work at logical stage
                return null;
            }

            setAsDispatchedHolder.PartitionKey = partitionKey;

            var outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, partitionKey, serializer, context)
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

            cosmosTransaction.StorageSession.AddOperation(new OutboxStore(new OutboxRecord
            {
                Id = message.MessageId,
                TransportOperations = message.TransportOperations
            },
                cosmosTransaction.PartitionKey.Value,
                serializer,
                context));
            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context)
        {
            var setAsDispatchedHolder = context.Get<SetAsDispatchedHolder>();

            var partitionKey = setAsDispatchedHolder.PartitionKey;
            var containerHolder = setAsDispatchedHolder.ContainerHolder;

            var operation = new OutboxDelete(new OutboxRecord
            {
                Id = messageId,
                Dispatched = true
            }, partitionKey, serializer, ttlInSeconds, context);

            var transactionalBatch = containerHolder.Container.CreateTransactionalBatch(partitionKey);

            await transactionalBatch.ExecuteOperationAsync(operation, containerHolder.PartitionKeyPath).ConfigureAwait(false);
        }

        readonly JsonSerializer serializer;
        readonly int ttlInSeconds;

        internal static readonly string SchemaVersion = "1.0.0";
        ContainerHolderResolver containerHolderResolver;
    }
}