namespace NServiceBus.Persistence.CosmosDB;

using System.Collections.Generic;
using System.IO;
using Microsoft.Azure.Cosmos;

abstract class UserOperation(TransactionalBatchItemRequestOptions requestOptions, PartitionKey partitionKey)
    : Operation(partitionKey, null, null)
{
    protected readonly TransactionalBatchItemRequestOptions requestOptions = requestOptions;
}

abstract class StreamUserOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    protected readonly Stream streamPayload = streamPayload;

    public override void Dispose() => streamPayload.Dispose();
}

sealed class CreateItemOperation<T>(T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.CreateItem(item, requestOptions);
}

sealed class CreateItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : StreamUserOperation(streamPayload, options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.CreateItemStream(streamPayload, requestOptions);
}

sealed class ReadItemOperation(string id, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.ReadItem(id, requestOptions);
}

sealed class UpsertItemOperation<T>(T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.UpsertItem(item, requestOptions);
}

sealed class UpsertItemStreamOperation(Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : StreamUserOperation(streamPayload, options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.UpsertItemStream(streamPayload, requestOptions);
}

sealed class ReplaceItemOperation<T>(string id, T item, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.ReplaceItem(id, item, requestOptions);
}

sealed class ReplaceItemStreamOperation(string id, Stream streamPayload, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : StreamUserOperation(streamPayload, options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.ReplaceItemStream(id, streamPayload, requestOptions);
}

sealed class DeleteItemOperation(string id, TransactionalBatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.DeleteItem(id, requestOptions);
}

sealed class PatchItemOperation(string id, IReadOnlyList<PatchOperation> patchOperations, TransactionalBatchPatchItemRequestOptions options, PartitionKey partitionKey)
    : UserOperation(options, partitionKey)
{
    public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => transactionalBatch.PatchItem(id, patchOperations, (TransactionalBatchPatchItemRequestOptions)requestOptions);
}