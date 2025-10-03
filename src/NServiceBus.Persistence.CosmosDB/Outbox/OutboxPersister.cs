namespace NServiceBus.Persistence.CosmosDB
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Outbox;
    using Transport;
    using Headers = NServiceBus.Headers;

    class OutboxPersister : IOutboxStorage
    {
        public OutboxPersister(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, string partitionKeyString, bool readFallbackEnabled, int ttlInSeconds)
        {
            this.containerHolderResolver = containerHolderResolver;
            this.serializer = serializer;
            this.ttlInSeconds = ttlInSeconds;
            this.partitionKeyString = partitionKeyString;
            this.readFallbackEnabled = readFallbackEnabled;
        }

        public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
        {
            var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

            if (context.TryGet<PartitionKey>(out var contextPartitionKey))
            {
                cosmosOutboxTransaction.PartitionKey = contextPartitionKey;
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

            if (!context.TryGet<PartitionKey>(out var contextPartitionKey))
            {
                // because of the transactional session we cannot assume the incoming message is always present
                if (!context.TryGet<IncomingMessage>(out var incomingMessage) ||
                    !incomingMessage.Headers.ContainsKey(Headers.ControlMessageHeader))
                {
                    // we return null here to enable outbox work at logical stage
                    return null;
                }

                // for control messages, use the synthetic partition key strategy to avoid concurreny conflicts
                // in pub sub scenarios
                contextPartitionKey = new PartitionKey($"{partitionKeyString}-{messageId}");
                context.Set(contextPartitionKey);
            }

            setAsDispatchedHolder.ThrowIfContainerIsNotSet();
            setAsDispatchedHolder.PartitionKey = contextPartitionKey;

            var outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, contextPartitionKey, serializer, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Only attempt the fallback if the user has the readFallbackEnabled flag is set.
            if (outboxRecord is null && readFallbackEnabled)
            {
                // fallback to the legacy single ID if the record wasn't found by the synthetic ID
                var fallbackPartitionKey = new PartitionKey(messageId);
                outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, fallbackPartitionKey, serializer, cancellationToken)
                    .ConfigureAwait(false);

                if (outboxRecord is not null)
                {
                    context.Set(fallbackPartitionKey);
                    setAsDispatchedHolder.PartitionKey = fallbackPartitionKey;
                }
            }

            return outboxRecord != null ? new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations?.Select(op => op.ToTransportType()).ToArray()) : null;
        }

        public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            var cosmosTransaction = (CosmosOutboxTransaction)transaction;

            if (cosmosTransaction == null || cosmosTransaction.AbandonStoreAndCommit || cosmosTransaction.PartitionKey == null)
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
            setAsDispatchedHolder.ThrowIfContainerIsNotSet();

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
        readonly string partitionKeyString;
        readonly bool readFallbackEnabled;

        internal static readonly string SchemaVersion = "1.0.0";
        ContainerHolderResolver containerHolderResolver;
    }
}