namespace NServiceBus.Persistence.CosmosDB.TransactionalSession;

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Pipeline;

class CosmosControlMessageBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
{
    public const string PartitionKeyStringHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKeyString";
    public const string PartitionKeyDoubleHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKeyDouble";
    public const string ContainerNameHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerName";
    public const string ContainerPartitionKeyPathHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerPartitionKeyPath";

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
    public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
    {
        if (context.Message.Headers.TryGetValue(PartitionKeyStringHeaderKey, out var partitionKeyString))
        {
            PartitionKey key;
            JToken jToken = JArray.Parse(partitionKeyString).First;

            if (jToken.Type == JTokenType.String)
            {
                key = new PartitionKey(jToken.Value<string>());
            }
            else if (jToken.Type == JTokenType.Boolean)
            {
                key = new PartitionKey(jToken.Value<bool>());
            }
            else if (jToken.Type == JTokenType.Float)
            {
                key = new PartitionKey(jToken.Value<double>());
            }
            else
            {
                throw new InvalidOperationException("TODO");
            }

            context.Extensions.Set(key);
        }

        if (context.Message.Headers.TryGetValue(ContainerNameHeaderKey, out string containerName)
            && context.Message.Headers.TryGetValue(ContainerPartitionKeyPathHeaderKey, out string partitionKeyPath))
        {
            var containerInformationInstance = new ContainerInformation(containerName, new PartitionKeyPath(partitionKeyPath));
            context.Extensions.Set(containerInformationInstance);
        }

        return next(context);
    }
}