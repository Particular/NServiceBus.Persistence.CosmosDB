namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    sealed class SharedTransactionalBatch : TransactionalBatch
    {
        readonly IWorkWithSharedTransactionalBatch operationsHolder;
        readonly PartitionKey partitionKey;

        public SharedTransactionalBatch(IWorkWithSharedTransactionalBatch operationsHolder, PartitionKey partitionKey)
        {
            this.operationsHolder = operationsHolder;
            this.partitionKey = partitionKey;
        }

        public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new CreateItemOperation<T>(item, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new CreateItemStreamOperation(streamPayload, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);

            operationsHolder.AddOperation(new ReadItemOperation(id, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new UpsertItemOperation<T>(item, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new UpsertItemStreamOperation(streamPayload, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);
            Guard.AgainstNull(nameof(item), item);

            operationsHolder.AddOperation(new ReplaceItemOperation<T>(id, item, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);
            Guard.AgainstNull(nameof(streamPayload), streamPayload);

            operationsHolder.AddOperation(new ReplaceItemStreamOperation(id, streamPayload, requestOptions, partitionKey));
            return this;
        }

        public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Guard.AgainstNullAndEmpty(nameof(id), id);

            operationsHolder.AddOperation(new DeleteItemOperation(id, requestOptions, partitionKey));
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
    }
}