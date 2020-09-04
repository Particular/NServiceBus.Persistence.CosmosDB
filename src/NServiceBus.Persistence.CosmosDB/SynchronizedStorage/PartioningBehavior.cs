namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Outbox;
    using Outbox;
    using Performance.TimeToBeReceived;
    using Pipeline;
    using Routing;
    using Transport;
    using TransportOperation = Transport.TransportOperation;

    class PartitioningBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        public PartitioningBehavior(string databaseName, CosmosClient cosmosClient, PartitionAwareConfiguration partitionAwareConfiguration)
        {
            this.databaseName = databaseName;
            this.cosmosClient = cosmosClient;
            this.partitionAwareConfiguration = partitionAwareConfiguration;
        }

        public async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            var incomingMessage = context.Extensions.Get<IncomingMessage>();
            var logicalMessage = context.Extensions.Get<LogicalMessage>();

            var partitionKey = partitionAwareConfiguration.MapMessageToPartition(incomingMessage.Headers, incomingMessage.MessageId, logicalMessage.MessageType, logicalMessage.Instance);
            var containerName = partitionAwareConfiguration.MapMessageToContainer(logicalMessage.MessageType);
            var partitionKeyPath = partitionAwareConfiguration.MapMessageToPartitionKeyPath(logicalMessage.MessageType);
            var container = cosmosClient.GetContainer(databaseName, containerName);

            context.Extensions.Set(partitionKey);
            context.Extensions.Set(container);
            context.Extensions.Set(ContextBagKeys.PartitionKeyPath, partitionKeyPath);
            context.Extensions.Set(ContextBagKeys.LogicalMessageId, context.MessageId);

            if (!(context.Extensions.Get<OutboxTransaction>() is CosmosOutboxTransaction outboxTransaction))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            OutboxRecord outboxRecord = await container.ReadItemAsync<OutboxRecord>(context.MessageId, partitionKey).ConfigureAwait(false);
            if (outboxRecord is null)
            {
                outboxTransaction.StorageSession = new StorageSession(container, partitionKey, partitionKeyPath, false);

                await next(context).ConfigureAwait(false);
                return;
            }

            var pendingTransportOperations = context.Extensions.Get<PendingTransportOperations>();

            //TODO: use reflection to clear any existing operations from previous behaviors created by the customer that sent or published a message not using immediate dispatch

            foreach (var operation in outboxRecord.TransportOperations)
            {
                var message = new OutgoingMessage(operation.MessageId, operation.Headers, operation.Body);

                pendingTransportOperations.Add(
                    new TransportOperation(
                        message,
                        DeserializeRoutingStrategy(operation.Options),
                        DispatchConsistency.Isolated,
                        DeserializeConstraints(operation.Options)));
            }
        }

        static List<DeliveryConstraint> DeserializeConstraints(Dictionary<string, string> options)
        {
            var constraints = new List<DeliveryConstraint>(4);
            if (options.ContainsKey("NonDurable"))
            {
                constraints.Add(new NonDurableDelivery());
            }

            if (options.TryGetValue("DeliverAt", out var deliverAt))
            {
                constraints.Add(new DoNotDeliverBefore(DateTimeExtensions.ToUtcDateTime(deliverAt)));
            }

            if (options.TryGetValue("DelayDeliveryFor", out var delay))
            {
                constraints.Add(new DelayDeliveryWith(TimeSpan.Parse(delay)));
            }

            if (options.TryGetValue("TimeToBeReceived", out var ttbr))
            {
                constraints.Add(new DiscardIfNotReceivedBefore(TimeSpan.Parse(ttbr)));
            }

            return constraints;
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

            throw new Exception("Could not find routing strategy to deserialize");
        }

        readonly string databaseName;
        readonly CosmosClient cosmosClient;
        readonly PartitionAwareConfiguration partitionAwareConfiguration;
    }
}