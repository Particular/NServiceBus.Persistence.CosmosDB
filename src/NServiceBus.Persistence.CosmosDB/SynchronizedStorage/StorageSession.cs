namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    class StorageSession : CompletableSynchronizedStorageSession
    {
        TransactionalBatchDecorator transactionalBatch;
        public Container Container { get; }

        public TransactionalBatch TransactionalBatch
        {
            get
            {
                if (transactionalBatch == null)
                {
                    transactionalBatch = new TransactionalBatchDecorator(Container.CreateTransactionalBatch(PartitionKey));
                }

                return transactionalBatch;
            }
        }
        public PartitionKey PartitionKey { get; }

        public StorageSession(Container container, PartitionKey partitionKey)
        {
            Container = container;
            PartitionKey = partitionKey;
        }

        public async Task CompleteAsync()
        {
            if (transactionalBatch == null)
            {
                return;
            }

            using (var batchOutcomeResponse = await TransactionalBatch.ExecuteAsync().ConfigureAwait(false))
            {
                foreach (var result in batchOutcomeResponse)
                {
                    if (result.StatusCode == HttpStatusCode.Conflict || result.StatusCode == HttpStatusCode.PreconditionFailed)
                    {
                        // technically would could somehow map back to what we wrote if we store extra info in the session
                        throw new Exception("Concurrent updates lead to write conflicts.");
                    }

                    // check retry after as well and implement retry if needed
                }
            }
        }

        public void Dispose()
        {
            transactionalBatch?.Dispose();
        }

        // Just a first version. Probably not even close to what we actually need to track etag etc.
        sealed class TransactionalBatchDecorator : TransactionalBatch, IDisposable
        {
            TransactionalBatch decorated;
            List<IDisposable> disposables = new List<IDisposable>();

            public TransactionalBatchDecorator(TransactionalBatch decorated)
            {
                this.decorated = decorated;
            }

            public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                return decorated.CreateItem(item, requestOptions);
            }

            public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                var result = decorated.CreateItemStream(streamPayload, requestOptions);
                disposables.Add(streamPayload);
                return result;
            }

            public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                return decorated.ReadItem(id, requestOptions);
            }

            public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                return decorated.UpsertItem(item, requestOptions);
            }

            public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                var result = decorated.UpsertItemStream(streamPayload, requestOptions);
                disposables.Add(streamPayload);
                return result;
            }

            public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                return decorated.ReplaceItem(id, item, requestOptions);
            }

            public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                var result = decorated.ReplaceItemStream(id, streamPayload, requestOptions);
                disposables.Add(streamPayload);
                return result;
            }

            public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                return decorated.DeleteItem(id, requestOptions);
            }

            public override Task<TransactionalBatchResponse> ExecuteAsync(CancellationToken cancellationToken = new CancellationToken())
            {
                return decorated.ExecuteAsync(cancellationToken);
            }

            public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken())
            {
                return decorated.ExecuteAsync(requestOptions, cancellationToken);
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
}