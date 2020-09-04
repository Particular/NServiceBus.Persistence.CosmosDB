namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Outbox;
    using Outbox;
    using Performance.TimeToBeReceived;
    using Pipeline;
    using Routing;
    using Transport;
    using TransportOperation = Transport.TransportOperation;

    class PartitioningBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        static PartitioningBehavior()
        {
            var field = typeof(PendingTransportOperations).GetField("operations", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetExp = Expression.Parameter(typeof(PendingTransportOperations), "target");
            var fieldExp = Expression.Field(targetExp, field);
            var assignExp = Expression.Assign(fieldExp, Expression.Constant(new ConcurrentStack<TransportOperation>()));

            setter = Expression.Lambda<Action<PendingTransportOperations>>(assignExp, targetExp).Compile();
        }

        public PartitioningBehavior(JsonSerializerSettings jsonSerializerSettings, string databaseName, CosmosClient cosmosClient, PartitionAwareConfiguration partitionAwareConfiguration)
        {
            this.databaseName = databaseName;
            this.cosmosClient = cosmosClient;
            this.partitionAwareConfiguration = partitionAwareConfiguration;
            serializer = JsonSerializer.Create(jsonSerializerSettings);
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

            if (!context.Extensions.TryGet<OutboxTransaction>(out var transaction))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!(transaction is CosmosOutboxTransaction outboxTransaction))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var outboxRecord = await container.ReadOutboxRecord(context.MessageId, partitionKey, serializer, context.Extensions)
                .ConfigureAwait(false);

            if (outboxRecord is null)
            {
                outboxTransaction.StorageSession = new StorageSession(container, partitionKey, partitionKeyPath, false);

                await next(context).ConfigureAwait(false);
                return;
            }

            var pendingTransportOperations = context.Extensions.Get<PendingTransportOperations>();
            setter(pendingTransportOperations);

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
        JsonSerializer serializer;
        static Action<PendingTransportOperations> setter;
    }
}