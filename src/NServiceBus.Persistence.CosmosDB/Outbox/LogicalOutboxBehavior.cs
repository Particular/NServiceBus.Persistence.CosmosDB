namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Outbox;
using Pipeline;
using Routing;
using Transport;
using TransportOperation = Transport.TransportOperation;

/// <summary>
/// Mimics the outbox behavior as part of the logical phase.
/// </summary>
class LogicalOutboxBehavior(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, bool readFallbackEnabled)
    : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
{
    /// <inheritdoc />
    public async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
    {
        if (!context.Extensions.TryGet(out IOutboxTransaction transaction))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (transaction is not CosmosOutboxTransaction outboxTransaction)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Normal outbox operating at the physical stage
        if (outboxTransaction.PartitionKey.HasValue)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Outbox operating at the logical stage
        if (!context.Extensions.TryGet(out PartitionKey partitionKey))
        {
            throw new Exception($"For the outbox to work a partition key must be provided either in the incoming physical or at latest in the logical message stage. Set one via '{nameof(CosmosPersistenceConfig.TransactionInformation)}'.");
        }

        ContainerHolder containerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context.Extensions);

        SetAsDispatchedHolder setAsDispatchedHolder = context.Extensions.Get<SetAsDispatchedHolder>();
        setAsDispatchedHolder.PartitionKey = partitionKey;
        setAsDispatchedHolder.ContainerHolder = containerHolder;

        outboxTransaction.PartitionKey = partitionKey;
        outboxTransaction.StorageSession.ContainerHolder = containerHolder;

        setAsDispatchedHolder.ThrowIfContainerIsNotSet();

        OutboxRecord outboxRecord = await containerHolder.Container.ReadOutboxRecord(context.MessageId, outboxTransaction.PartitionKey.Value, serializer, context.CancellationToken)
            .ConfigureAwait(false);

        // Only attempt the fallback if the readFallbackEnabled flag is set.
        if (outboxRecord is null && readFallbackEnabled)
        {
            // fallback to the legacy single ID if the record wasn't found by the synthetic ID
            var fallbackPartitionKey = new PartitionKey(context.MessageId);
            outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(context.MessageId, fallbackPartitionKey, serializer, context.CancellationToken)
                .ConfigureAwait(false);

            if (outboxRecord is not null)
            {
                setAsDispatchedHolder.PartitionKey = fallbackPartitionKey;
                outboxTransaction.PartitionKey = fallbackPartitionKey;
            }
        }

        if (outboxRecord is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Signals that Outbox persister Store and Commit should be no-ops
        outboxTransaction.AbandonStoreAndCommit = true;

        PendingTransportOperations pendingTransportOperations = context.Extensions.Get<PendingTransportOperations>();
        pendingTransportOperations.Clear();

        foreach (StorageTransportOperation operation in outboxRecord.TransportOperations)
        {
            var message = new OutgoingMessage(operation.MessageId, operation.Headers, operation.Body);

            pendingTransportOperations.Add(
                new TransportOperation(
                    message,
                    DeserializeRoutingStrategy(operation.Options),
                    new DispatchProperties(operation.Options),
                    DispatchConsistency.Isolated));
        }
    }

    static AddressTag DeserializeRoutingStrategy(Dictionary<string, string> options)
    {
        if (options.TryGetValue("Destination", out string destination))
        {
            return new UnicastAddressTag(destination);
        }

        if (options.TryGetValue("EventType", out string eventType))
        {
            return new MulticastAddressTag(Type.GetType(eventType, true));
        }

        throw new Exception("Could not find routing strategy to deserialize.");
    }
}