namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NServiceBus.Transport;
using Outbox;

class OutboxPersister(ContainerHolderResolver containerHolderResolver, JsonSerializer serializer, string partitionKey, bool readFallbackEnabled, TransactionInformationConfiguration transactionConfiguration, int ttlInSeconds)
    : IOutboxStorage
{
    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        var cosmosOutboxTransaction = new CosmosOutboxTransaction(containerHolderResolver, context);

        if (context.TryGet(out PartitionKey partitionKey))
        {
            // Only set partition key if we won't defer
            if (!transactionConfiguration.HasCustomContainerMessageExtractors)
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

        var finalPartitionKey = PartitionKey.Null;
        bool shouldDeferToLogicalStage = false;

        // Determine if we should defer to logical stage for partition key extraction
        if (!havePartitionKeyInContext)
        {
            // If this is a control message but still doesnt have a partition key at this point,
            // we need to set it to the default synthetic key.
            if (context.TryGet(out IncomingMessage incomingMessage) &&
                incomingMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                // Use default synthetic partition key
                finalPartitionKey = GetPartitionKey(extractedPartitionKey, messageId);
            }
            else if (transactionConfiguration.HasCustomPartitionMessageExtractors)
            {
                // Custom partition key extractors need the message body
                shouldDeferToLogicalStage = true;
            }
            else if (transactionConfiguration.HasCustomPartitionHeaderExtractors)
            {
                // If we dont have a partition key here, but expect to via a custom header extractor, we need to throw
                throw new Exception($"For the outbox to work a partition key must be provided either in the incoming physical or at latest in the logical message stage. Set one via '{nameof(CosmosPersistenceConfig.TransactionInformation)}'.");
            }
            else if (!transactionConfiguration.HasAnyCustomPartitionExtractors)
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

        // Check if we need to defer for container extraction. If both a header and message extractor is added, we defer to logical stage.
        if ((!setAsDispatchedHolder.ContainerIsSet() && transactionConfiguration.HasCustomContainerMessageExtractors) ||
            (transactionConfiguration.HasCustomContainerMessageExtractors && transactionConfiguration.HasCustomContainerHeaderExtractors))
        {
            // When deferring for container extraction, ensure partition key is set in context
            // Use the default synthetic key if no custom partition extractors are configured
            if (!havePartitionKeyInContext && !transactionConfiguration.HasAnyCustomPartitionExtractors)
            {
                context.Set(finalPartitionKey);
            }

            shouldDeferToLogicalStage = true;
        }

        // If we determined we should defer for partition key extraction, do so now
        if (shouldDeferToLogicalStage)
        {
            return null;
        }

        setAsDispatchedHolder.ThrowIfContainerIsNotSet();

        // If the user has overridden the default synthetic partition key strategy, then partitionKeyObject will be what they have set. If not, it will be the synthetic.
        OutboxRecord outboxRecord = await setAsDispatchedHolder.ContainerHolder.Container.ReadOutboxRecord(messageId, finalPartitionKey, serializer, cancellationToken)
            .ConfigureAwait(false);

        // Only attempt the fallback if the user has NOT overridden the partition key strategy and the readFallbackEnabled flag is set.
        // There's no point in trying to fallback if the user has specified their own partition key strategy and the record wasn't found.
        // This saves an unnecessary read.
        if (outboxRecord is null && readFallbackEnabled && !transactionConfiguration.HasAnyCustomPartitionExtractors)
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