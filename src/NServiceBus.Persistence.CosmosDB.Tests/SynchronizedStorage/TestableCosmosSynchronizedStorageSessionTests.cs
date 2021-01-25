namespace NServiceBus.Persistence.CosmosDB.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class TestableCosmosSynchronizedStorageSessionTests
    {
        [Test]
        public async Task CanBeUsed()
        {
            var transactionalBatch = new FakeTransactionalBatch();

            var testableSession = new TestableCosmosSynchronizedStorageSession(new PartitionKey("mypartitionkey"))
            {
                TransactionalBatch = transactionalBatch
            };
            var handlerContext = new TestableInvokeHandlerContext
            {
                SynchronizedStorageSession = testableSession
            };

            var handler = new HandlerUsingSynchronizedStorageSessionExtension();
            await handler.Handle(new MyMessage(), handlerContext);

            Assert.IsNotEmpty(transactionalBatch.CreatedItems.OfType<MyItem>());
        }

        class FakeTransactionalBatch : TransactionalBatch
        {
            public List<object> CreatedItems { get; } = new List<object>();

            public override TransactionalBatch CreateItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                CreatedItems.Add(item);
                return this;
            }

            public override TransactionalBatch CreateItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch ReadItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch UpsertItem<T>(T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch UpsertItemStream(Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch ReplaceItem<T>(string id, T item, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch ReplaceItemStream(string id, Stream streamPayload, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override TransactionalBatch DeleteItem(string id, TransactionalBatchItemRequestOptions requestOptions = null)
            {
                throw new System.NotImplementedException();
            }

            public override Task<TransactionalBatchResponse> ExecuteAsync(CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }

            public override Task<TransactionalBatchResponse> ExecuteAsync(TransactionalBatchRequestOptions requestOptions, CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }
        }

        class HandlerUsingSynchronizedStorageSessionExtension : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var session = context.SynchronizedStorageSession.CosmosPersistenceSession();
                var myItem = new MyItem();
                session.Batch.CreateItem(myItem);
                return Task.CompletedTask;
            }
        }

        class MyItem
        {
            public string Id { get; set; }
        }

        class MyMessage { }
    }
}