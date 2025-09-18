namespace NServiceBus.Persistence.CosmosDB;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Outbox;
using Transport;

class OutboxPersister(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, string partitionKey, bool readFallbackEnabled, ExtractorConfiguration extractorConfig, int ttlInSeconds)
    : IOutboxStorage
{
    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

        if (context.TryGet(out PartitionKey partitionKey))
        {
            cosmosOutboxTransaction.PartitionKey = partitionKey;
        }

        return Task.FromResult((IOutboxTransaction)cosmosOutboxTransaction);
    }

    public async Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        var setAsDispatchedHolder = new SetAsDispatchedHolder { ContainerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context) };
        context.Set(setAsDispatchedHolder);

        if (!context.TryGet(out PartitionKey _))
        {
            // If the partition key is not present in the context at the physical stage (headers), and
            // a custom message PK extractor is configured, it means the PK is expected to be extracted from the message body
            // and we need to defer the read to the logical stage.
            if (extractorConfig.HasCustomPartitionMessageExtractors)
            {
                return null;
            }

            // because of the transactional session we cannot assume the incoming message is always present
            // If there is an incoming message, check if it's a control message. If it is, defer the read to the logical stage.
            // Otherwise, proceed to set the default synthetic PK strategy.
            if (context.TryGet(out IncomingMessage incomingMessage))
            {
                // if the incoming message is a control message, defer the read to the logical stage
                // as control messages don't have the user message body needed for physical extraction
                if (incomingMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
                {
                    // we return null here to enable outbox work at logical stage
                    return null;
                }
            }
        }

        // Set the final partition key to be used. Either default synthetic or custom extracted.
        var finalPartitionKey = GetPartitionKey(messageId, context);
        //context.Set(finalPartitionKey);

        if (!setAsDispatchedHolder.ContainerIsSet() && extractorConfig.HasCustomContainerMessageExtractors)
        {
            return null;
        }

        // TODO: throw if the container doesnt exist in cosmosDB. This can happen if the user has a custom extractor that extracts a container that doesnt exist.
        setAsDispatchedHolder.ThrowIfContainerIsNotSet();

        // If the user has overridden the default synthetic partition key strategy, then partitionKeyObject will be what they have set. If not, it will be the synthetic.
        OutboxRecord outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, finalPartitionKey, serializer, cancellationToken)
            .ConfigureAwait(false);

        // Only attempt the fallback if the user has NOT overridden the partition key strategy and the readFallbackEnabled flag is set.
        // Theres no point in trying to fallback if the user has specified their own partition key strategy and the record wasn't found.
        // This saves an unnecessary read.
        if (outboxRecord is null && readFallbackEnabled && !extractorConfig.HasAnyCustomPartitionExtractors)
        {
            // fallback to the legacy single ID if the record wasn't found by the synthetic ID
            var fallbackPartitionKey = new PartitionKey(messageId);
            outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, fallbackPartitionKey, serializer, cancellationToken)
                .ConfigureAwait(false);

            if (outboxRecord is not null)
            {
                finalPartitionKey = fallbackPartitionKey;
                context.Set(fallbackPartitionKey);
            }
        }

        context.Set(finalPartitionKey);
        setAsDispatchedHolder.PartitionKey = finalPartitionKey;

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

    PartitionKey GetPartitionKey(string messageId, ContextBag context)
    {
        PartitionKey finalPartitionKey;

        // Is the user overriding the default synthetic partition key strategy using custom partition key extractors?
        if (extractorConfig.HasAnyCustomPartitionExtractors) // yes
        {
            // The user is trying to extract a custom partition key using their extractors. Does the key exist in the context?
            var hasCustomPartitionKey = context.TryGet(out PartitionKey partitionKeyObject);
            if (!hasCustomPartitionKey) // no
            {
                // if we reach here, it means the user has custom extractors but none could extract a partition key. e.g. incorrect header name
                // TODO: default to synthetic strategy instead? Or throw? Defaulting will hide the fallback which is not ideal.
                finalPartitionKey = new PartitionKey($"{partitionKey}-{messageId}");
            }
            else // yes
            {
                // found a custom partition key
                finalPartitionKey = partitionKeyObject;
            }
        }
        else // no
        {
            // use the synthetic partition key strategy when custom extractors are not used (default).
            finalPartitionKey = new PartitionKey($"{partitionKey}-{messageId}");
        }

        return finalPartitionKey;
    }

    internal static readonly string SchemaVersion = "1.0.0";
}