namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using Outbox;
    using Transport;
    using Headers = NServiceBus.Headers;

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
                // because of the transactional session we cannot assume the incoming message is always present
                if (!context.TryGet<IncomingMessage>(out var incomingMessage) ||
                    !incomingMessage.Headers.ContainsKey(Headers.ControlMessageHeader))
                {
                    // we return null here to enable outbox work at logical stage
                    return null;
                }

                partitionKey = new PartitionKey(messageId);
                context.Set(partitionKey);
            }

            setAsDispatchedHolder.ThrowIfContainerIsNotSet();
            setAsDispatchedHolder.PartitionKey = partitionKey;

            var outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, partitionKey, serializer, context, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var logResult = outboxRecord == null ? "null" : $"{{ Dispatched={outboxRecord.Dispatched}, TxOps.Length={outboxRecord.TransportOperations.Length} }}";
            log.Info($"CosmosDB:Outbox:Get, MessageId='{messageId}', Container='{setAsDispatchedHolder.ContainerHolder.Container.Id}', PartitionKey={partitionKey}, Result={logResult}");

            if (outboxRecord == null)
            {
                return null;
            }

            var outboxMessage = new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations?.Select(op => op.ToTransportType()).ToArray());
            context.Set("TempDebugOutboxOutgoingTransportOperations", outboxMessage.TransportOperations);
            return outboxMessage;
        }

        public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
        {
            var cosmosTransaction = (CosmosOutboxTransaction)transaction;

            var logTx = (cosmosTransaction == null) ? "null" : $"Tx = {{ AbandonStoreAndCommit={cosmosTransaction.AbandonStoreAndCommit}, PartitionKey={cosmosTransaction.PartitionKey}, Container='{cosmosTransaction.StorageSession.Container.Id}' }}";
            log.Info($"CosmosDB:Outbox:Store, MessageId='{message.MessageId}, TxOps.Length={message.TransportOperations.Length}, Transaction = {logTx}");

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

            context.Set("TempDebugOutboxOutgoingTransportOperations", message.TransportOperations);

            return Task.CompletedTask;
        }

        public async Task SetAsDispatched(string messageId, ContextBag context, CancellationToken cancellationToken = default)
        {
            var setAsDispatchedHolder = context.Get<SetAsDispatchedHolder>();
            setAsDispatchedHolder.ThrowIfContainerIsNotSet();

            var partitionKey = setAsDispatchedHolder.PartitionKey;
            var containerHolder = setAsDispatchedHolder.ContainerHolder;

            if (!context.TryGet<NServiceBus.Outbox.TransportOperation[]>("TempDebugOutboxOutgoingTransportOperations", out var transportOperations))
            {
                transportOperations = Array.Empty<NServiceBus.Outbox.TransportOperation>();
            }

            log.Info($"CosmosDB:Outbox:SetAsDispatched, MessageId='{messageId}, PartitionKey={partitionKey}, Container='{containerHolder.Container.Id}'");

            var operation = new OutboxDelete(new OutboxRecord
            {
                Id = messageId,
                Dispatched = true,
                TransportOperations = transportOperations.Select(op => new StorageTransportOperation(op)).ToArray()
            }, partitionKey, serializer, ttlInSeconds, context);

            var transactionalBatch = containerHolder.Container.CreateTransactionalBatch(partitionKey);

            await transactionalBatch.ExecuteOperationAsync(operation, containerHolder.PartitionKeyPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        readonly JsonSerializer serializer;
        readonly int ttlInSeconds;

        static readonly ILog log = LogManager.GetLogger<OutboxPersister>();

        internal static readonly string SchemaVersion = "1.0.0";
        ContainerHolderResolver containerHolderResolver;
    }
}