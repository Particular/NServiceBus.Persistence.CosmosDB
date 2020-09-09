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

            // Normal outbox operating at the physical stage
            if (outboxTransaction.PartitionKey.HasValue)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Outbox operating at the logical stage
            if (!context.Extensions.TryGet<PartitionKey>(out var partitionKey))
            {
                throw new Exception("For the outbox to work the following information must be provided at latest up to the incoming physical or logical message stage. A partition key via `context.Extensions.Set<PartitionKey>(yourPartitionKey)`");
            }

            outboxTransaction.PartitionKey = partitionKey;

            var container = context.Extensions.Get<Container>();

            var outboxRecord = await container.ReadOutboxRecord(context.MessageId, outboxTransaction.PartitionKey.Value, serializer, context.Extensions)
                .ConfigureAwait(false);

            if (outboxRecord is null)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Signals that Outbox persister Store and Commit should be no-ops
            outboxTransaction.SuppressStoreAndCommit = true;

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