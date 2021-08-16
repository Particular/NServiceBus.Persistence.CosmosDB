namespace NServiceBus.Persistence.CosmosDB
{
    using System.Linq;
    using System.Threading;
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

        public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
        {
            var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

            if (context.TryGet<PartitionKey>(out var partitionKey))
            {
                cosmosOutboxTransaction.PartitionKey = partitionKey;
            }

            return Task.FromResult((IOutboxTransaction)cosmosOutboxTransaction);
        }

        public async Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
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

            var outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, partitionKey, serializer, context, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return outboxRecord != null ? new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations?.Select(op => op.ToTransportType()).ToArray()) : null;
        }

        public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            var cosmosTransaction = (CosmosOutboxTransaction)transaction;

            if (cosmosTransaction == null || cosmosTransaction.SuppressStoreAndCommit || cosmosTransaction.PartitionKey == null)
            {
                return Task.CompletedTask;
            }

            cosmosTransaction.StorageSession.AddOperation(new OutboxStore(new OutboxRecord
            {
                Id = message.MessageId,
                TransportOperations = message.TransportOperations.Select(op => new StorageTransportOperation(op)).ToArray()
            },
                cosmosTransaction.PartitionKey.Value,
                serializer,
                context));
            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context, CancellationToken cancellationToken = default)
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

            await transactionalBatch.ExecuteOperationAsync(operation, containerHolder.PartitionKeyPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        readonly JsonSerializer serializer;
        readonly int ttlInSeconds;

        internal static readonly string SchemaVersion = "1.0.0";
        ContainerHolderResolver containerHolderResolver;
    }
}