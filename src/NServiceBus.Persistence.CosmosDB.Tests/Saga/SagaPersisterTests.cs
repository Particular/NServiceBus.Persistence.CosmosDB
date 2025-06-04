namespace NServiceBus.Persistence.CosmosDB.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Scripts;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class SagaPersisterTests
    {
        [Test]
        public async Task Should_return_null_when_not_found()
        {
            var fakeContainer = new FakeContainer
            {
                ReadItemStreamOutboxRecord = () => new ResponseMessage(HttpStatusCode.NotFound)
            };

            var partitionKey = new PartitionKey("somePartitionKey");

            var synchronizedStorageSession = new TestableCosmosSynchronizedStorageSession(partitionKey) { Container = fakeContainer, };

            var persister = new SagaPersister(JsonSerializer.Create(), new SagaPersistenceConfiguration());

            var contextBag = new ContextBag();
            contextBag.Set(partitionKey);

            var sagaData = await persister.Get<TestSagaData>(Guid.NewGuid(), synchronizedStorageSession, contextBag);

            Assert.That(sagaData, Is.Null);
        }

        [Test]
        public void Should_rethrow_unsuccessful_status()
        {
            var fakeContainer = new FakeContainer
            {
                // not testing more status codes since we would effectively be testing EnsureSuccessfulStatus
                ReadItemStreamOutboxRecord = () => new ResponseMessage(HttpStatusCode.TooManyRequests)
            };

            var partitionKey = new PartitionKey("somePartitionKey");

            var synchronizedStorageSession = new TestableCosmosSynchronizedStorageSession(partitionKey) { Container = fakeContainer, };

            var persister = new SagaPersister(JsonSerializer.Create(), new SagaPersistenceConfiguration());

            var contextBag = new ContextBag();
            contextBag.Set(partitionKey);

            Assert.ThrowsAsync<CosmosException>(async () => await persister.Get<TestSagaData>(Guid.NewGuid(), synchronizedStorageSession, contextBag));
        }

        class TestSagaData : ContainSagaData
        {
            public string SomeProperty { get; set; }
        }

        class FakeContainer : Container
        {
            public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new()) => Task.FromResult(ReadItemStreamOutboxRecord());

            public Func<ResponseMessage> ReadItemStreamOutboxRecord = () => new ResponseMessage(HttpStatusCode.OK);

            #region Not Implemented Members

            public override string Id => throw new NotImplementedException();

            public override Database Database => throw new NotImplementedException();

            public override Conflicts Conflicts => throw new NotImplementedException();

            public override Scripts Scripts => throw new NotImplementedException();

            public override Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> CreateItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey) => throw new NotImplementedException();
            public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> PatchItemStreamAsync(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new()) => throw new NotImplementedException();
            public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override ChangeFeedEstimator GetChangeFeedEstimator(string processorName, Container leaseContainer) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName, ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod = null) => throw new NotImplementedException();
            public override FeedIterator<T> GetChangeFeedIterator<T>(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode, ChangeFeedRequestOptions changeFeedRequestOptions = null) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangeFeedHandler<T> onChangesDelegate) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(string processorName, ChangeFeedStreamHandler onChangesDelegate) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(string processorName, ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate) => throw new NotImplementedException();
            public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(string processorName, ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate) => throw new NotImplementedException();
            public override FeedIterator GetChangeFeedStreamIterator(ChangeFeedStartFrom changeFeedStartFrom, ChangeFeedMode changeFeedMode, ChangeFeedRequestOptions changeFeedRequestOptions = null) => throw new NotImplementedException();
            public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public override FeedIterator<T> GetItemQueryIterator<T>(FeedRange feedRange, QueryDefinition queryDefinition,
                string continuationToken = null, QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null, QueryRequestOptions requestOptions = null, CosmosLinqSerializerOptions linqSerializerOptions = null) => throw new NotImplementedException();
            public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
            public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();

            public override FeedIterator GetItemQueryStreamIterator(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken,
                QueryRequestOptions requestOptions = null) =>
                throw new NotImplementedException();

            public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
            public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
            public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ItemResponse<T>> PatchItemAsync<T>(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new()) => throw new NotImplementedException();
            public override Task<ResponseMessage> ReadManyItemsStreamAsync(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> ReplaceItemStreamAsync(Stream streamPayload, string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public override Task<ResponseMessage> UpsertItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            #endregion
        }
    }
}