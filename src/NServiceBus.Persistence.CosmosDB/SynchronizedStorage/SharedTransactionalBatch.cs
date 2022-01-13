namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    // Makes sure delegate all operations to the storage session. Nothing is executed yet to avoid running into transaction timeouts.
    sealed class SharedTransactionalBatch : TransactionalBatch, ICosmosStorageSession
    {
        public SharedTransactionalBatch(IWorkWithSharedTransactionalBatch operationsHolder, PartitionKey partitionKey)
        {
            this.operationsHolder = operationsHolder;
            PartitionKey = partitionKey;
        }

        public PartitionKey PartitionKey { get; }

        public Container Container => operationsHolder.Container;
        public PartitionKeyPath PartitionKeyPath => operationsHolder.PartitionKeyPath;

        public TransactionalBatch Batch => this;

        public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new CreateItemOperation<T>(item, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new CreateItemStreamOperation(streamPayload, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);

            operationsHolder.AddOperation(new ReadItemOperation(id, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new UpsertItemOperation<T>(item, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new UpsertItemStreamOperation(streamPayload, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new ReplaceItemOperation<T>(id, item, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new ReplaceItemStreamOperation(id, streamPayload, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);

            operationsHolder.AddOperation(new DeleteItemOperation(id, requestOptions, PartitionKey));
            return this;
        }

        public override TransactionalBatch PatchItem(string id, IReadOnlyList<PatchOperation> patchOperations, TransactionalBatchPatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);
            Guard.AgainstNull(nameof(patchOperations), patchOperations);

            operationsHolder.AddOperation(new PatchItemOperation(id, patchOperations, requestOptions, PartitionKey));
            return this;
        }

        public override Task<TransactionalBatchResponse> ExecuteAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new InvalidOperationException("Storage Session will execute the transaction");
        }

        public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new InvalidOperationException("Storage Session will execute the transaction");
        }

        readonly IWorkWithSharedTransactionalBatch operationsHolder;
    }
}