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
    using NServiceBus.Outbox;
    using NUnit.Framework;
    using Outbox;
    using Pipeline;
    using Settings;
    using Testing;
    using Transport;
    using Unicast.Messages;
    using TransportOperation = Transport.TransportOperation;

    [TestFixture]
    public class PartitioningBehaviorTests
    {
        [Test]
        public async Task Should_clear_added_pending_operations_and_restore_ones_from_outbox_record()
        {
            var persistenceExtensions = new PersistenceExtensions<CosmosDbPersistence>(new SettingsHolder());
            var partitionAwareConfiguration = persistenceExtensions.Partition();
            partitionAwareConfiguration.AddPartitionMappingForMessageType<object>((h, id, m) => new PartitionKey(""), "", "");

            var messageId = Guid.NewGuid().ToString();

            var fakeCosmosClient = new FakeCosmosClient();
            fakeCosmosClient.Container.ReadItemStreamOutboxRecord = (id, key) => new OutboxRecord
            {
                Dispatched = false,
                Id = messageId,
                TransportOperations = new []
                {
                    new NServiceBus.Outbox.TransportOperation("42", new Dictionary<string, string>
                    {
                        { "Destination", "somewhere" }
                    }, Array.Empty<byte>(), new Dictionary<string, string>()),
                }
            };

            var behavior = new PartitioningBehavior(new JsonSerializer());

            var testableContext = new TestableIncomingLogicalMessageContext();

            testableContext.Extensions.Set(new IncomingMessage(messageId, new Dictionary<string, string>(), Array.Empty<byte>()));
            testableContext.Extensions.Set(new LogicalMessage(new MessageMetadata(typeof(object)), null));
            testableContext.Extensions.Set<OutboxTransaction>(new CosmosOutboxTransaction(new FakeContainer()));

            var pendingTransportOperations = new PendingTransportOperations();
            pendingTransportOperations.Add(new TransportOperation(new OutgoingMessage(null, null, null), null));
            testableContext.Extensions.Set(pendingTransportOperations);

            await behavior.Invoke(testableContext, c => Task.CompletedTask);

            Assert.IsTrue(pendingTransportOperations.HasOperations, "Should have exactly one operation added found on the outbox record");
            Assert.AreEqual("42", pendingTransportOperations.Operations.ElementAt(0).Message.MessageId, "Should have exactly one operation added found on the outbox record");
        }
    }

    class FakeOutboxTransaction : OutboxTransaction
    {
        public void Dispose()
        {
        }

        public Task Commit()
        {
            return Task.CompletedTask;
        }
    }

    class FakeCosmosClient : CosmosClient
    {
        public FakeContainer Container { get; set; } = new FakeContainer();

        public override Container GetContainer(string databaseId, string containerId)
        {
            return Container;
        }
    }

    class FakeContainer : Container
    {
        public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties containerProperties, ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> CreateItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Func<string, PartitionKey, OutboxRecord> ReadItemStreamOutboxRecord = (id, key) => new OutboxRecord();

        public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            var responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ReadItemStreamOutboxRecord(id, partitionKey))))
            };
            return Task.FromResult(responseMessage);
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> UpsertItemStreamAsync(Stream streamPayload, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> ReplaceItemStreamAsync(Stream streamPayload, string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey = null, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions = null, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        public override FeedIterator GetItemQueryStreamIterator(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(string queryText = null, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false, string continuationToken = null, QueryRequestOptions requestOptions = null)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate)
        {
            throw new NotImplementedException();
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName, ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod = null)
        {
            throw new NotImplementedException();
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            throw new NotImplementedException();
        }

        public override string Id { get; }
        public override Database Database { get; }
        public override Conflicts Conflicts { get; }
        public override Scripts Scripts { get; }
    }
}