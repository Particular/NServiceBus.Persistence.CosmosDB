namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

abstract class SagaOperation(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
    : Operation(partitionKey, serializer, context)
{
    protected readonly IContainSagaData sagaData = sagaData;
    protected Stream stream = Stream.Null;

    static ConcurrentDictionary<Type, JObject> sagaMetaDataCache = new();

    public override void Success(TransactionalBatchOperationResult result)
    {
        base.Success(result);

        Context.Set($"cosmos_etag:{sagaData.Id}", result.ETag ?? string.Empty);
    }

    protected JObject ToEnrichedJObject(PartitionKeyPath partitionKeyPath)
    {
        var jObject = JObject.FromObject(sagaData, Serializer);

        JObject metadata;
        if (Context.TryGet($"cosmos_migratedsagaid:{sagaData.Id}", out Guid migratedSagaId))
        {
            metadata = CreateMetadata(sagaData.GetType());
            metadata.Add(MetadataExtensions.SagaDataContainerMigratedSagaIdMetadataKey, migratedSagaId);
        }
        else
        {
            // in the case it is not a migrated saga the metadata can be shared since it is the same per saga data type
            // the value factory is not thread safe but that is OK since the object is not heavy to create
            metadata = sagaMetaDataCache.GetOrAdd(sagaData.GetType(), type => CreateMetadata(type));
        }

        jObject.Add(MetadataExtensions.MetadataKey, metadata);

        EnrichWithPartitionKeyIfNecessary(jObject, partitionKeyPath);

        return jObject;
    }

    static JObject CreateMetadata(Type sagaDataType) =>
        new()
        {
            { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaSchemaVersion.Current },
            { MetadataExtensions.SagaDataContainerFullTypeNameMetadataKey, sagaDataType.FullName }
        };

    public override void Dispose() => stream.Dispose();
}

sealed class SagaSave(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
    : SagaOperation(sagaData, partitionKey, serializer, context)
{
    public override void Conflict(TransactionalBatchOperationResult result) => throw new TransactionalBatchOperationException($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' could not be created. Response status code: {result.StatusCode}.", result);

    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
    {
        JObject jObject = ToEnrichedJObject(partitionKeyPath);

        // Has to be kept open for transaction batch to be able to use the stream
        stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
        var options = new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false };
        transactionalBatch.CreateItemStream(stream, options);
    }
}

sealed class SagaUpdate(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
    : SagaOperation(sagaData, partitionKey, serializer, context)
{
    public override void Conflict(TransactionalBatchOperationResult result) => throw new TransactionalBatchOperationException($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' could not be updated. Response status code: {result.StatusCode}.", result);

    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
    {
        JObject jObject = ToEnrichedJObject(partitionKeyPath);

        // only update if we have the same version as in CosmosDB
        Context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var updateEtag);
        var options = new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = updateEtag,
            EnableContentResponseOnWrite = false
        };

        // has to be kept open
        stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
        transactionalBatch.ReplaceItemStream(sagaData.Id.ToString(), stream, options);
    }
}

sealed class SagaDelete(IContainSagaData sagaData, PartitionKey partitionKey, ContextBag context)
    : SagaOperation(sagaData, partitionKey, null, context)
{
    public override void Conflict(TransactionalBatchOperationResult result) => throw new TransactionalBatchOperationException($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' could not be completed. Response status code: {result.StatusCode}.", result);

    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
    {
        // only delete if we have the same version as in CosmosDB
        Context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var deleteEtag);
        var deleteOptions = new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = deleteEtag,
            EnableContentResponseOnWrite = false
        };
        transactionalBatch.DeleteItem(sagaData.Id.ToString(), deleteOptions);
    }
}

// Special cleanup operation that only gets executed for pessimistic locking
sealed class SagaReleaseLock(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
    : SagaOperation(sagaData, partitionKey, serializer, context), IReleaseLockOperation
{
    static IReadOnlyList<PatchOperation> CleanupPatchOperations
    {
        get;
    } = [PatchOperation.Remove($"/{MetadataExtensions.MetadataKey}/{MetadataExtensions.SagaDataContainerReservedUntilMetadataKey}")];

    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
    {
        // only update if we have the same version as in CosmosDB
        Context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var updateEtag);
        var requestOptions = new TransactionalBatchPatchItemRequestOptions
        {
            IfMatchEtag = updateEtag,
            EnableContentResponseOnWrite = false
        };

        transactionalBatch.PatchItem(sagaData.Id.ToString(), CleanupPatchOperations, requestOptions);
    }

    public override void Conflict(TransactionalBatchOperationResult result)
    {
        // we do not care about possible conflicts since the cleanup is best effort
    }
}