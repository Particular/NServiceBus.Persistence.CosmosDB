namespace NServiceBus.Persistence.CosmosDB;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Outbox;
using Transport;

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

        if (context.TryGet<PartitionKey>(out PartitionKey partitionKey))
        {
            cosmosOutboxTransaction.PartitionKey = partitionKey;
        }

        return Task.FromResult((IOutboxTransaction)cosmosOutboxTransaction);
    }

    public async Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        var setAsDispatchedHolder = new SetAsDispatchedHolder { ContainerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context) };
        context.Set(setAsDispatchedHolder);

        if (!context.TryGet<PartitionKey>(out PartitionKey partitionKey))
        {
            // because of the transactional session we cannot assume the incoming message is always present
            if (!context.TryGet<IncomingMessage>(out IncomingMessage incomingMessage) ||
                !incomingMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                // we return null here to enable outbox work at logical stage
                return null;
            }

            partitionKey = new PartitionKey(messageId);
            context.Set(partitionKey);
        }

        setAsDispatchedHolder.ThrowIfContainerIsNotSet();
        setAsDispatchedHolder.PartitionKey = partitionKey;

        OutboxRecord outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, partitionKey, serializer, context, cancellationToken)
            .ConfigureAwait(false);

        return outboxRecord != null ? new OutboxMessage(outboxRecord.Id, outboxRecord.TransportOperations?.Select(op => op.ToTransportType()).ToArray()) : null;
    }

    public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
    {
        var cosmosTransaction = (CosmosOutboxTransaction)transaction;

        if (cosmosTransaction == null || cosmosTransaction.AbandonStoreAndCommit || cosmosTransaction.PartitionKey == null)
        {
            return Task.CompletedTask;
        }

        cosmosTransaction.StorageSession.AddOperation(new OutboxStore(new OutboxRecord(id: message.MessageId, transportOperations: message.TransportOperations.Select(op => new StorageTransportOperation(op)).ToArray()),
            cosmosTransaction.PartitionKey.Value,
            serializer,
            context));
        return Task.CompletedTask;
    }

    public async Task SetAsDispatched(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        SetAsDispatchedHolder setAsDispatchedHolder = context.Get<SetAsDispatchedHolder>();
        setAsDispatchedHolder.ThrowIfContainerIsNotSet();

        PartitionKey partitionKey = setAsDispatchedHolder.PartitionKey;
        ContainerHolder containerHolder = setAsDispatchedHolder.ContainerHolder;

        var operation = new OutboxDelete(new OutboxRecord
        {
            Id = messageId,
            Dispatched = true
        }, partitionKey, serializer, ttlInSeconds, context);

        TransactionalBatch transactionalBatch = containerHolder.Container.CreateTransactionalBatch(partitionKey);

        await transactionalBatch.ExecuteOperationAsync(operation, containerHolder.PartitionKeyPath, cancellationToken).ConfigureAwait(false);
    }

    readonly JsonSerializer serializer;
    readonly int ttlInSeconds;

    internal static readonly string SchemaVersion = "1.0.0";
    ContainerHolderResolver containerHolderResolver;
}