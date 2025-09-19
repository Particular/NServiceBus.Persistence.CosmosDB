namespace NServiceBus.Persistence.CosmosDB;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Outbox;
using Transport;

class OutboxPersister(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, string partitionKey, bool readFallbackEnabled, ExtractorConfigurationHolder extractorConfigHolder, int ttlInSeconds)
    : IOutboxStorage
{
    readonly ExtractorConfiguration extractorConfig = extractorConfigHolder?.Configuration ?? new ExtractorConfiguration();

    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

        // Only set partition key if:
        // 1. We have one in context AND
        // 2. We're not going to defer to logical stage for container extraction
        if (context.TryGet(out PartitionKey partitionKey))
        {
            // Check if we'll defer for container extraction
            var containerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context);
            bool willDeferForContainer = containerHolder == null && extractorConfig.HasCustomContainerMessageExtractors;

            // Only set partition key if we won't defer
            if (!willDeferForContainer)
            {
                cosmosOutboxTransaction.PartitionKey = partitionKey;
            }
        }

        return Task.FromResult((IOutboxTransaction)cosmosOutboxTransaction);
    }

    public async Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        var setAsDispatchedHolder = new SetAsDispatchedHolder
        {
            ContainerHolder = containerHolderResolver.ResolveAndSetIfAvailable(context)
        };
        context.Set(setAsDispatchedHolder);

        var havePartitionKeyInContext = context.TryGet(out PartitionKey extractedPartitionKey);
        PartitionKey finalPartitionKey = PartitionKey.Null;
        bool shouldDeferToLogicalStage = false;

        // Determine if we should defer to logical stage for partition key extraction
        if (!havePartitionKeyInContext)
        {
            if (extractorConfig.HasCustomPartitionMessageExtractors)
            {
                // Custom partition key extractors need the message body
                shouldDeferToLogicalStage = true;
            }
            else if (context.TryGet(out IncomingMessage incomingMessage) &&
                     incomingMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                // Control messages need to defer to logical stage
                shouldDeferToLogicalStage = true;
            }
            else if (!extractorConfig.HasAnyCustomPartitionExtractors)
            {
                // Use default synthetic partition key
                finalPartitionKey = GetPartitionKey(extractedPartitionKey, messageId);
            }
        }
        else
        {
            // We have a partition key from context (headers or previous extraction)
            finalPartitionKey = extractedPartitionKey;
        }

        // Check if we need to defer for container extraction
        if (!setAsDispatchedHolder.ContainerIsSet() && extractorConfig.HasCustomContainerMessageExtractors)
        {
            // When deferring for container extraction, ensure partition key is set in context
            // Use the default synthetic key if no custom partition extractors are configured
            if (!havePartitionKeyInContext && !extractorConfig.HasAnyCustomPartitionExtractors)
            {
                finalPartitionKey = GetPartitionKey(extractedPartitionKey, messageId);
                context.Set(finalPartitionKey);
            }
            return null;
        }

        // If we determined we should defer for partition key extraction, do so now
        if (shouldDeferToLogicalStage)
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
            }
        }

        // Ensure the final partition key is set in context
        if (finalPartitionKey != PartitionKey.Null)
        {
            context.Set(finalPartitionKey);
        }
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
    PartitionKey GetPartitionKey(PartitionKey extractedPartitionKey, string messageId)
    {
        // If we have an extracted partition key (from headers or message body), use it.
        if (extractedPartitionKey != PartitionKey.Null)
        {
            return extractedPartitionKey;
        }
        else
        {
            // Use the default synthetic partition key strategy when no extracted PK is present.
            return new PartitionKey($"{partitionKey}-{messageId}");
        }
    }

    internal static readonly string SchemaVersion = "1.0.0";
}