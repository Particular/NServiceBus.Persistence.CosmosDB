namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
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

        public PartitioningBehavior(JsonSerializerSettings jsonSerializerSettings)
        {
            serializer = JsonSerializer.Create(jsonSerializerSettings);
        }

        public async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
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

            var partitionKeyFound = context.Extensions.TryGet<PartitionKey>(out var partitionKey);
            var partitionKeyPathFound = context.Extensions.TryGet<PartitionKeyPath>(out var partitionKeyPath);
            var containerFound = context.Extensions.TryGet<Container>(out var container);

            if (!partitionKeyFound || !containerFound || !partitionKeyPathFound)
            {
                var messageBuilder = new StringBuilder("For the outbox to work the following information must be provided at latest up to the incoming logical message stage:");
                if (!partitionKeyFound)
                {
                    messageBuilder.AppendLine("- A partition key via `context.Extensions.Set<PartitionKey>(yourPartitionKey)`");
                }

                if (!partitionKeyPathFound)
                {
                    messageBuilder.AppendLine("- A partition key path via `context.Extensions.Set<PartitionKeyPath>(yourPartitionKeyPath)`");
                }

                if (!containerFound)
                {
                    messageBuilder.AppendLine("- A container via `context.Extensions.Set<Container>(yourContainer)`");
                }

                throw new Exception(messageBuilder.ToString());
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

        JsonSerializer serializer;
        static Action<PendingTransportOperations> setter;
    }
}