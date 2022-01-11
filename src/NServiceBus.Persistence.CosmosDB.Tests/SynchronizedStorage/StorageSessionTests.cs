namespace NServiceBus.Persistence.CosmosDB.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Scripts;
    using NUnit.Framework;

    [TestFixture]
    public class StorageSessionTests
    {
        [Test]
        public async Task Should_not_complete_when_marked_as_do_not_complete()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), false);
            var firstOperation = new FakeOperation();
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation();
            storageSession.AddOperation(secondOperation);

            await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();

            Assert.That(firstOperation.WasDisposed, Is.False);
            Assert.That(firstOperation.WasApplied, Is.False);
            Assert.That(secondOperation.WasDisposed, Is.False);
            Assert.That(secondOperation.WasApplied, Is.False);
        }

        [Test]
        public void Should_throw_when_no_container_available()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var operation = new FakeOperation();
            storageSession.AddOperation(operation);

            var exception = Assert.ThrowsAsync<Exception>(async () => await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync());
            Assert.That(exception.Message, Is.EqualTo("Unable to retrieve the container name and the partition key during processing. Make sure that either `persistence.Container()` is used or the relevant container information is available on the message handling pipeline."));
        }

        [Test]
        public void Should_dispose_operations()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstOperation = new FakeOperation();
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation();
            storageSession.AddOperation(secondOperation);

            storageSession.Dispose();

            Assert.That(firstOperation.WasDisposed, Is.True);
            Assert.That(firstOperation.WasApplied, Is.False);
            Assert.That(secondOperation.WasDisposed, Is.True);
            Assert.That(secondOperation.WasApplied, Is.False);
        }

        [Test]
        public async Task Should_execute_operations_with_same_partition_key_together()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstOperation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(secondOperation);

            await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();

            Assert.That(firstOperation.WasApplied, Is.True);
            Assert.That(secondOperation.WasApplied, Is.True);
            Assert.That(firstOperation.AppliedBatch, Is.EqualTo(secondOperation.AppliedBatch), "Operations with the same partition key must be in the same batch");
        }

        [Test]
        public async Task Should_execute_operations_with_different_partition_key_distinct()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstOperation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey2")
            };
            storageSession.AddOperation(secondOperation);

            await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();

            Assert.That(firstOperation.WasApplied, Is.True);
            Assert.That(secondOperation.WasApplied, Is.True);
            Assert.That(firstOperation.AppliedBatch, Is.Not.EqualTo(secondOperation.AppliedBatch), "Operations with the different partition keys cannot be in the same batch");
        }

        [Test]
        public async Task Should_dispose_release_operations_when_operations_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var operation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(operation);
            var releaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(releaseOperation);

            await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();

            Assert.That(operation.WasApplied, Is.True);
            Assert.That(operation.WasDisposed, Is.False);
            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_not_execute_release_operations_when_operations_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var operation = new FakeOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(operation);
            var releaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(releaseOperation);

            await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();
            storageSession.Dispose();

            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public void Should_execute_and_dispose_release_operations_with_same_partition_key_together_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(firstReleaseOperation);

            var secondReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(secondReleaseOperation);

            storageSession.Dispose();

            Assert.That(firstReleaseOperation.WasApplied, Is.True);
            Assert.That(secondReleaseOperation.WasApplied, Is.True);
            Assert.That(firstReleaseOperation.WasDisposed, Is.True);
            Assert.That(secondReleaseOperation.WasDisposed, Is.True);
            Assert.That(firstReleaseOperation.AppliedBatch, Is.EqualTo(secondReleaseOperation.AppliedBatch), "Release operations with the same partition key must be in the same batch");
        }

        [Test]
        public void Should_execute_and_dispose_release_operations_as_best_effort()
        {
            var fakeContainer = new FakeContainer
            {
                TransactionalBatchFactory = () => new ThrowsOnExecuteAsyncTransactionalBatch()
            };
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(firstReleaseOperation);

            var secondReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey2")
            };
            storageSession.AddOperation(secondReleaseOperation);

            Assert.DoesNotThrow(() => storageSession.Dispose());

            Assert.That(firstReleaseOperation.WasApplied, Is.True);
            Assert.That(secondReleaseOperation.WasApplied, Is.True);
            Assert.That(firstReleaseOperation.WasDisposed, Is.True);
            Assert.That(secondReleaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public void Should_execute_and_dispose_release_operations_with_different_partition_key_distinct_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var firstReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(firstReleaseOperation);

            var secondReleaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey2")
            };
            storageSession.AddOperation(secondReleaseOperation);

            storageSession.Dispose();

            Assert.That(firstReleaseOperation.WasApplied, Is.True);
            Assert.That(secondReleaseOperation.WasApplied, Is.True);
            Assert.That(firstReleaseOperation.WasDisposed, Is.True);
            Assert.That(secondReleaseOperation.WasDisposed, Is.True);
            Assert.That(firstReleaseOperation.AppliedBatch, Is.Not.EqualTo(secondReleaseOperation.AppliedBatch), "Release operations with the different partition keys must be in different batches.");
        }

        [Test]
        public async Task Should_not_dispose_release_operations_when_operations_not_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new StorageSession(containerHolderHolderResolver, new ContextBag(), true);
            var operation = new ThrowOnApplyOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(operation);
            var releaseOperation = new ReleaseLockOperation
            {
                PartitionKey = new PartitionKey("PartitionKey1")
            };
            storageSession.AddOperation(releaseOperation);

            try
            {
                await ((ICompletableSynchronizedStorageSession)storageSession).CompleteAsync();
            }
            catch
            {
                // ignored
            }

            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.False);
        }

        class ThrowOnApplyOperation : FakeOperation
        {
            public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath) => throw new InvalidOperationException();
        }

        class ReleaseLockOperation : FakeOperation, IReleaseLockOperation
        {
        }

        class FakeOperation : IOperation
        {
            public bool WasDisposed { get; private set; }
            public bool WasApplied { get; private set; }

            public TransactionalBatch AppliedBatch { get; private set; }

            public void Dispose() => WasDisposed = true;

            public PartitionKey PartitionKey { get; set; }
            public virtual void Success(TransactionalBatchOperationResult result) => throw new System.NotImplementedException();

            public virtual void Conflict(TransactionalBatchOperationResult result) => throw new System.NotImplementedException();

            public virtual void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
            {
                WasApplied = true;
                AppliedBatch = transactionalBatch;
            }
        }

        class FakeTransactionalBatch : TransactionalBatch
        {
            public override Task<TransactionalBatchResponse> ExecuteAsync(
                CancellationToken cancellationToken = new CancellationToken()) =>
                Task.FromResult<TransactionalBatchResponse>(new FakeTransactionalBatchResponse());

            #region Not Important
            public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload,
                TransactionalBatchItemRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch PatchItem(string id, IReadOnlyList<PatchOperation> patchOperations,
                TransactionalBatchPatchItemRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();
            #endregion
        }

        class ThrowsOnExecuteAsyncTransactionalBatch : TransactionalBatch
        {
            public override Task<TransactionalBatchResponse> ExecuteAsync(
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new InvalidOperationException();

            #region Not Important
            public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload,
                TransactionalBatchItemRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override TransactionalBatch PatchItem(string id, IReadOnlyList<PatchOperation> patchOperations,
                TransactionalBatchPatchItemRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();
            #endregion
        }

        class FakeTransactionalBatchResponse : TransactionalBatchResponse
        {
        }

        class FakeContainer : Container
        {
            public Func<TransactionalBatch> TransactionalBatchFactory = () => new FakeTransactionalBatch();

            public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey) => TransactionalBatchFactory();

            #region Not Important
            public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

            public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();


            public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> CreateItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> UpsertItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> ReplaceItemStreamAsync(Stream streamPayload, string id, PartitionKey partitionKey,
                ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> ReadManyItemsStreamAsync(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> PatchItemAsync<T>(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations,
                PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> PatchItemStreamAsync(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations,
                PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = new CancellationToken()) =>
                throw new NotImplementedException();

            public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null,
                QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null,
                QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null,
                QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null,
                QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null,
                QueryRequestOptions requestOptions = null, CosmosLinqSerializerOptions linqSerializerOptions = null) =>
                throw new NotImplementedException();

            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate) => throw new NotImplementedException();


            public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName,
                ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod = null) =>
                throw new NotImplementedException();

            public override ChangeFeedEstimator GetChangeFeedEstimator(string processorName, Container leaseContainer) => throw new NotImplementedException();


            public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

            public override FeedIterator GetChangeFeedStreamIterator(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode,
                ChangeFeedRequestOptions changeFeedRequestOptions = null) =>
                throw new NotImplementedException();

            public override FeedIterator<T> GetChangeFeedIterator<T>(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode,
                ChangeFeedRequestOptions changeFeedRequestOptions = null) =>
                throw new NotImplementedException();

            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangeFeedHandler<T> onChangesDelegate) => throw new NotImplementedException();

            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(string processorName,
                ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate) =>
                throw new NotImplementedException();

            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(string processorName,
                ChangeFeedStreamHandler onChangesDelegate) =>
                throw new NotImplementedException();

            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(string processorName,
                ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate) =>
                throw new NotImplementedException();

            public override string Id { get; }
            public override Database Database { get; }
            public override Conflicts Conflicts { get; }
            public override Scripts Scripts { get; }
            #endregion
        }
    }
}