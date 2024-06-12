namespace NServiceBus.Persistence.CosmosDB
{
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
    class LogicalOutboxBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        internal LogicalOutboxBehavior(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer)
        {
            this.containerHolderResolver = containerHolderResolver;
            this.serializer = serializer;
        }

        /// <inheritdoc />
        public async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            if (!context.Extensions.TryGet<IOutboxTransaction>(out var transaction))
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
            if (!context.Extensions.TryGet<PartitionKey>(out var partitionKey))
            {
                throw new Exception($"For the outbox to work a partition key must be provided either in the incoming physical or at latest in the logical message stage. Set one via '{nameof(CosmosPersistenceConfig.TransactionInformation)}'.");
            }

            var containerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context.Extensions);

            var setAsDispatchedHolder = context.Extensions.Get<SetAsDispatchedHolder>();
            setAsDispatchedHolder.PartitionKey = partitionKey;
            setAsDispatchedHolder.ContainerHolder = containerHolder;

            outboxTransaction.PartitionKey = partitionKey;
            outboxTransaction.StorageSession.ContainerHolder = containerHolder;

            setAsDispatchedHolder.ThrowIfContainerIsNotSet();

            var outboxRecord = await containerHolder.Container.ReadOutboxRecord(context.MessageId, outboxTransaction.PartitionKey.Value, serializer, context.Extensions, context.CancellationToken)
                .ConfigureAwait(false);

            if (outboxRecord is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Signals that Outbox persister Store and Commit should be no-ops
            outboxTransaction.AbandonStoreAndCommit = true;

            var pendingTransportOperations = context.Extensions.Get<PendingTransportOperations>();
            pendingTransportOperations.Clear();

            foreach (var operation in outboxRecord.TransportOperations)
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
            if (options.TryGetValue("Destination", out var destination))
            {
                return new UnicastAddressTag(destination);
            }

            if (options.TryGetValue("EventType", out var eventType))
            {
                return new MulticastAddressTag(Type.GetType(eventType, true));
            }

            throw new Exception("Could not find routing strategy to deserialize.");
        }

        readonly JsonSerializer serializer;
        readonly ContainerHolderResolver containerHolderResolver;
    }
}