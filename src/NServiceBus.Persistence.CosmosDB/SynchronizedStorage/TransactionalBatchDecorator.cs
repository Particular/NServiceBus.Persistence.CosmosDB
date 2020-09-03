namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    sealed class TransactionalBatchDecorator : TransactionalBatch, IDisposable
    {
        List<IDisposable> disposables = new List<IDisposable>();

        public int Index { get; private set; } = -1;
        public TransactionalBatch Inner { get; }

        public bool CanBeExecuted => Index > -1;

        public TransactionalBatchDecorator(TransactionalBatch decorated)
        {
            Inner = decorated;
        }

        public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            return Inner.CreateItem(item, requestOptions);
        }

        public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            var result = Inner.CreateItemStream(streamPayload, requestOptions);
            disposables.Add(streamPayload);
            return result;
        }

        public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            return Inner.ReadItem(id, requestOptions);
        }

        public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            return Inner.UpsertItem(item, requestOptions);
        }

        public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            var result = Inner.UpsertItemStream(streamPayload, requestOptions);
            disposables.Add(streamPayload);
            return result;
        }

        public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            return Inner.ReplaceItem(id, item, requestOptions);
        }

        public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            var result = Inner.ReplaceItemStream(id, streamPayload, requestOptions);
            disposables.Add(streamPayload);
            return result;
        }

        public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
        {
            Index++;
            return Inner.DeleteItem(id, requestOptions);
        }

        public override Task<TransactionalBatchResponse> ExecuteAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new InvalidOperationException("Storage Session will execute the transaction");
        }

        public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new InvalidOperationException("Storage Session will execute the transaction");
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            disposables.Clear();
        }
    }
}