namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.DelayedDelivery;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Outbox;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class PartitioningBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        readonly string databaseName;
        readonly CosmosClient cosmosClient;
        readonly PartitionAwareConfiguration partitionAwareConfiguration;

        public PartitioningBehavior(string databaseName, CosmosClient cosmosClient, PartitionAwareConfiguration partitionAwareConfiguration)
        {
            this.databaseName = databaseName;
            this.cosmosClient = cosmosClient;
            this.partitionAwareConfiguration = partitionAwareConfiguration;
        }

        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            var outboxTransaction = context.Extensions.Get<OutboxTransaction>() as CosmosOutboxTransaction;

            if (outboxTransaction is null)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var incomingMessage = context.Extensions.Get<IncomingMessage>();
            var logicalMessage = context.Extensions.Get<LogicalMessage>();

            var partitionKey = partitionAwareConfiguration.MapMessageToPartition(incomingMessage.Headers, incomingMessage.MessageId, logicalMessage.MessageType, logicalMessage.Instance);
            var containerName = partitionAwareConfiguration.MapMessageToContainer(logicalMessage.MessageType);
            var partitionKeyPath = partitionAwareConfiguration.MapMessageToPartitionKeyPath(logicalMessage.MessageType);
            var container = cosmosClient.GetContainer(databaseName, containerName);

            context.Extensions.Set(ContextBagKeys.PartitionKeyValue, partitionKey);
            context.Extensions.Set(ContextBagKeys.PartitionKeyPath, partitionKeyPath);
            context.Extensions.Set(container);

            OutboxRecord outboxRecord = await container.ReadItemAsync<OutboxRecord>(context.MessageId, partitionKey).ConfigureAwait(false);

            if (outboxRecord is null)
            {
                outboxTransaction.TransactionalBatch = new TransactionalBatchDecorator(container.CreateTransactionalBatch(partitionKey));

                await next().ConfigureAwait(false);

                return;
            }

            var pendingTransportOperations = context.Extensions.Get<PendingTransportOperations>();

            //TODO: use reflection to clear any existing operations from previous behaviors created by the customer

            foreach (var operation in outboxRecord.TransportOperations)
            {
                var message = new OutgoingMessage(operation.MessageId, operation.Headers, operation.Body);

                pendingTransportOperations.Add(
                    new Transport.TransportOperation(
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
    }
}
