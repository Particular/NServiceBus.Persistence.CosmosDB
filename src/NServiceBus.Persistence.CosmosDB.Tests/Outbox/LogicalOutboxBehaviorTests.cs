namespace NServiceBus.Persistence.CosmosDB.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Scripts;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Outbox;
    using Testing;
    using Transport;
    using TransportOperation = Outbox.TransportOperation;

    [TestFixture]
    public class LogicalOutboxBehaviorTests
    {
        [Test]
        public async Task Should_clear_added_pending_operations_and_restore_ones_from_outbox_record()
        {
            var messageId = Guid.NewGuid().ToString();

            var transportOperations = new[]
            {
                new TransportOperation(
                    messageId: "42",
                    properties: new DispatchProperties { { "Destination" , "somewhere"} },
                    body: Array.Empty<byte>(),
                    headers: new Dictionary<string, string>()),
            };

            var fakeCosmosClient = new FakeCosmosClient
            {
                Container =
                {
                    ReadItemStreamOutboxRecord = (id, key) => new OutboxRecord
                    {
                        Dispatched = false,
                        Id = messageId,
                        TransportOperations = transportOperations.Select(op => new StorageTransportOperation(op)).ToArray()
                    }
                }
            };

            var containerHolderHolderResolver = new ContainerHolderResolver(new Provider()
            {
                Client = fakeCosmosClient
            },
                new ContainerInformation("fakeContainer",
                    new PartitionKeyPath("")),
                "fakeDatabase");

            var behavior = new LogicalOutboxBehavior(containerHolderHolderResolver, new JsonSerializer());

            var testableContext = new TestableIncomingLogicalMessageContext();

            testableContext.Extensions.Set(new PartitionKey(""));
            testableContext.Extensions.Set(new SetAsDispatchedHolder());

            testableContext.Extensions.Set<IOutboxTransaction>(new CosmosOutboxTransaction(containerHolderHolderResolver, testableContext.Extensions));

            var pendingTransportOperations = new PendingTransportOperations();
            pendingTransportOperations.Add(new Transport.TransportOperation(new OutgoingMessage(null, null, null), null));
            testableContext.Extensions.Set(pendingTransportOperations);

            await behavior.Invoke(testableContext, c => Task.CompletedTask);

            Assert.IsTrue(pendingTransportOperations.HasOperations, "Should have exactly one operation added found on the outbox record");
            Assert.AreEqual("42", pendingTransportOperations.Operations.ElementAt(0).Message.MessageId, "Should have exactly one operation added found on the outbox record");
        }
    }

    class Provider : IProvideCosmosClient
    {
        public CosmosClient Client { get; set; }
    }

    class FakeCosmosClient : CosmosClient
    {
        public FakeContainer Container { get; set; } = new FakeContainer();

        public override Container GetContainer(string databaseId, string containerId) => Container;
    }

    class FakeContainer : Container
    {
        public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ReadItemStreamOutboxRecord(id, partitionKey))))
            };
            return Task.FromResult(responseMessage);
        }

        public Func<string, PartitionKey, OutboxRecord> ReadItemStreamOutboxRecord = (id, key) => new OutboxRecord();

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
        public override Task<ResponseMessage> PatchItemStreamAsync(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();
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
        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null, QueryRequestOptions requestOptions = null, CosmosLinqSerializerOptions linqSerializerOptions = null) => throw new NotImplementedException();
        public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
        public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
        public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
        public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null) => throw new NotImplementedException();
        public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(IReadOnlyList<(string id, PartitionKey partitionKey)> items, ReadManyRequestOptions readManyRequestOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task<ItemResponse<T>> PatchItemAsync<T>(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> patchOperations, PatchItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();
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
    }
}
