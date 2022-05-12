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

    /// <summary>
    /// These tests verify the correct behavior between the outbox transaction and the completable storage session
    /// together because those two classes go hand in hand.
    /// </summary>
    [TestFixture]
    public class StorageSessionTests
    {
        [Test]
        public async Task Should_not_complete_when_owned_by_outbox_transaction()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

            var firstOperation = new FakeOperation();
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation();
            storageSession.AddOperation(secondOperation);

            await storageSession.CompleteAsync();
            storageSession.Dispose();

            Assert.That(firstOperation.WasDisposed, Is.False);
            Assert.That(firstOperation.WasApplied, Is.False);
            Assert.That(secondOperation.WasDisposed, Is.False);
            Assert.That(secondOperation.WasApplied, Is.False);
        }

        [Test]
        public async Task Should_complete_when_outbox_transaction_completes()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

            var firstOperation = new FakeOperation();
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation();
            storageSession.AddOperation(secondOperation);

            await storageSession.CompleteAsync();
            storageSession.Dispose();

            var firstOperationWasDisposed = firstOperation.WasDisposed;
            var firstOperationWasApplied = firstOperation.WasApplied;
            var secondOperationWasDisposed = secondOperation.WasDisposed;
            var secondOperationWasApplied = secondOperation.WasApplied;

            await outboxTransaction.Commit();
            outboxTransaction.Dispose();

            Assert.That(firstOperationWasDisposed, Is.False);
            Assert.That(firstOperationWasApplied, Is.False);
            Assert.That(secondOperationWasDisposed, Is.False);
            Assert.That(secondOperationWasApplied, Is.False);

            Assert.That(firstOperation.WasDisposed, Is.True);
            Assert.That(firstOperation.WasApplied, Is.True);
            Assert.That(secondOperation.WasDisposed, Is.True);
            Assert.That(secondOperation.WasApplied, Is.True);
        }

        [Test]
        public async Task Should_set_current_context_bag_to_inner_when_using_outbox_transaction()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransactionContextBag = new ContextBag();
            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, outboxTransactionContextBag);

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            var synchronizedStorageSessionContextBag = new ContextBag();
            await storageSession.TryOpen(outboxTransaction, synchronizedStorageSessionContextBag);

            Assert.AreSame(synchronizedStorageSessionContextBag, storageSession.CurrentContextBag);
        }

        [Test]
        public async Task Should_throw_on_complete_when_no_container_available()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

            storageSession.AddOperation(new FakeOperation());

            var exception = Assert.ThrowsAsync<Exception>(async () => await storageSession.CompleteAsync());
            Assert.That(exception.Message, Is.EqualTo("Unable to retrieve the container name and the partition key during processing. Make sure that either `persistence.Container()` is used or the relevant container information is available on the message handling pipeline."));
        }

        [Test]
        public async Task Should_throw_on_outbox_transaction_commit_when_no_container_available()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

            storageSession.AddOperation(new FakeOperation());

            Assert.DoesNotThrowAsync(() => storageSession.CompleteAsync());
            var exception = Assert.ThrowsAsync<Exception>(async () => await outboxTransaction.Commit());
            Assert.That(exception.Message, Is.EqualTo("Unable to retrieve the container name and the partition key during processing. Make sure that either `persistence.Container()` is used or the relevant container information is available on the message handling pipeline."));
        }

        [Test]
        public async Task Should_dispose_operations()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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
        public async Task Should_dispose_operations_when_outbox_transaction_disposes()
        {
            var fakeCosmosClient = new FakeCosmosClient(null);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient), null, "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

            var firstOperation = new FakeOperation();
            storageSession.AddOperation(firstOperation);
            var secondOperation = new FakeOperation();
            storageSession.AddOperation(secondOperation);
            storageSession.Dispose();

            var firstOperationWasDisposed = firstOperation.WasDisposed;
            var firstOperationWasApplied = firstOperation.WasApplied;
            var secondOperationWasDisposed = secondOperation.WasDisposed;
            var secondOperationWasApplied = secondOperation.WasApplied;

            outboxTransaction.Dispose();

            Assert.That(firstOperationWasDisposed, Is.False);
            Assert.That(firstOperationWasApplied, Is.False);
            Assert.That(secondOperationWasDisposed, Is.False);
            Assert.That(secondOperationWasApplied, Is.False);

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

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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

            await storageSession.CompleteAsync();

            Assert.That(firstOperation.WasApplied, Is.True);
            Assert.That(secondOperation.WasApplied, Is.True);
            Assert.That(firstOperation.AppliedBatch, Is.EqualTo(secondOperation.AppliedBatch), "Operations with the same partition key must be in the same batch");
        }

        [Test]
        public async Task Should_execute_with_outbox_transaction_operations_with_same_partition_key_together()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            await storageSession.CompleteAsync();

            var firstOperationWasApplied = firstOperation.WasApplied;
            var secondOperationWasApplied = secondOperation.WasApplied;
            var firstOperationAppliedBatch = firstOperation.AppliedBatch;
            var secondOperationAppliedBatch = secondOperation.AppliedBatch;

            await outboxTransaction.Commit();

            Assert.That(firstOperationWasApplied, Is.False);
            Assert.That(secondOperationWasApplied, Is.False);
            Assert.That(firstOperationAppliedBatch, Is.Null);
            Assert.That(secondOperationAppliedBatch, Is.Null);

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

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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

            await storageSession.CompleteAsync();

            Assert.That(firstOperation.WasApplied, Is.True);
            Assert.That(secondOperation.WasApplied, Is.True);
            Assert.That(firstOperation.AppliedBatch, Is.Not.EqualTo(secondOperation.AppliedBatch), "Operations with the different partition keys cannot be in the same batch");
        }

        [Test]
        public async Task Should_execute_with_outbox_transaction_operations_with_different_partition_key_distinct()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            await storageSession.CompleteAsync();

            var firstOperationWasApplied = firstOperation.WasApplied;
            var secondOperationWasApplied = secondOperation.WasApplied;
            var firstOperationAppliedBatch = firstOperation.AppliedBatch;
            var secondOperationAppliedBatch = secondOperation.AppliedBatch;

            await outboxTransaction.Commit();

            Assert.That(firstOperationWasApplied, Is.False);
            Assert.That(secondOperationWasApplied, Is.False);
            Assert.That(firstOperationAppliedBatch, Is.Null);
            Assert.That(secondOperationAppliedBatch, Is.Null);

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

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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

            await storageSession.CompleteAsync();

            Assert.That(operation.WasApplied, Is.True);
            Assert.That(operation.WasDisposed, Is.False);
            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_dispose_release_operations_on_outbox_transaction_commit_when_operations_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            await storageSession.CompleteAsync();

            var operationWasApplied = operation.WasApplied;
            var operationWasDisposed = operation.WasDisposed;
            var releaseOperationWasApplied = releaseOperation.WasApplied;
            var releaseOperationWasDisposed = releaseOperation.WasDisposed;

            await outboxTransaction.Commit();

            Assert.That(operationWasApplied, Is.False);
            Assert.That(operationWasDisposed, Is.False);
            Assert.That(releaseOperationWasApplied, Is.False);
            Assert.That(releaseOperationWasDisposed, Is.False);

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

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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

            await storageSession.CompleteAsync();
            storageSession.Dispose();

            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_not_execute_release_operations_on_outbox_transaction_commit_when_operations_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            await storageSession.CompleteAsync();
            storageSession.Dispose();

            var releaseOperationWasApplied = releaseOperation.WasApplied;
            var releaseOperationWasDisposed = releaseOperation.WasDisposed;

            await outboxTransaction.Commit();
            outboxTransaction.Dispose();

            Assert.That(releaseOperationWasApplied, Is.False);
            Assert.That(releaseOperationWasDisposed, Is.False);

            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_execute_and_dispose_release_operations_with_same_partition_key_together_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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
        public async Task Should_execute_and_dispose_release_operations_on_outbox_transaction_dispose_with_same_partition_key_together_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            var firstReleaseOperationWasApplied = firstReleaseOperation.WasApplied;
            var firstReleaseOperationWasDisposed = firstReleaseOperation.WasDisposed;
            var firstReleaseOperationAppliedBatch = firstReleaseOperation.AppliedBatch;
            var secondReleaseOperationWasApplied = secondReleaseOperation.WasApplied;
            var secondReleaseOperationWasDisposed = secondReleaseOperation.WasDisposed;
            var secondReleaseOperationAppliedBatch = secondReleaseOperation.AppliedBatch;

            outboxTransaction.Dispose();

            Assert.That(firstReleaseOperationWasApplied, Is.False);
            Assert.That(secondReleaseOperationWasApplied, Is.False);
            Assert.That(firstReleaseOperationWasDisposed, Is.False);
            Assert.That(secondReleaseOperationWasDisposed, Is.False);
            Assert.That(firstReleaseOperationAppliedBatch, Is.Null);
            Assert.That(secondReleaseOperationAppliedBatch, Is.Null);

            Assert.That(firstReleaseOperation.WasApplied, Is.True);
            Assert.That(secondReleaseOperation.WasApplied, Is.True);
            Assert.That(firstReleaseOperation.WasDisposed, Is.True);
            Assert.That(secondReleaseOperation.WasDisposed, Is.True);
            Assert.That(firstReleaseOperation.AppliedBatch, Is.EqualTo(secondReleaseOperation.AppliedBatch), "Release operations with the same partition key must be in the same batch");
        }

        [Test]
        public async Task Should_execute_and_dispose_release_operations_as_best_effort()
        {
            var fakeContainer = new FakeContainer
            {
                TransactionalBatchFactory = () => new ThrowsOnExecuteAsyncTransactionalBatch()
            };
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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
        public async Task Should_execute_and_dispose_release_operations_on_outbox_transaction_dispose_as_best_effort()
        {
            var fakeContainer = new FakeContainer
            {
                TransactionalBatchFactory = () => new ThrowsOnExecuteAsyncTransactionalBatch()
            };
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            Assert.DoesNotThrow(() => outboxTransaction.Dispose());

            Assert.That(firstReleaseOperation.WasApplied, Is.True);
            Assert.That(secondReleaseOperation.WasApplied, Is.True);
            Assert.That(firstReleaseOperation.WasDisposed, Is.True);
            Assert.That(secondReleaseOperation.WasDisposed, Is.True);
        }

        [Test]
        public async Task Should_execute_and_dispose_release_operations_with_different_partition_key_distinct_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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
        public async Task Should_execute_and_dispose_release_operations_on_outbox_transaction_dispose_with_different_partition_key_distinct_when_not_completed()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            var firstReleaseOperationWasApplied = firstReleaseOperation.WasApplied;
            var firstReleaseOperationWasDisposed = firstReleaseOperation.WasDisposed;
            var firstReleaseOperationAppliedBatch = firstReleaseOperation.AppliedBatch;
            var secondReleaseOperationWasApplied = secondReleaseOperation.WasApplied;
            var secondReleaseOperationWasDisposed = secondReleaseOperation.WasDisposed;
            var secondReleaseOperationAppliedBatch = secondReleaseOperation.AppliedBatch;

            outboxTransaction.Dispose();

            Assert.That(firstReleaseOperationWasApplied, Is.False);
            Assert.That(secondReleaseOperationWasApplied, Is.False);
            Assert.That(firstReleaseOperationWasDisposed, Is.False);
            Assert.That(secondReleaseOperationWasDisposed, Is.False);
            Assert.That(firstReleaseOperationAppliedBatch, Is.Null);
            Assert.That(secondReleaseOperationAppliedBatch, Is.Null);

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

            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.Open(new ContextBag());

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

            Assert.That(async () => await storageSession.CompleteAsync(), Throws.Exception);
            Assert.That(releaseOperation.WasApplied, Is.False);
            Assert.That(releaseOperation.WasDisposed, Is.False);
        }

        [Test]
        public async Task Should_not_dispose_release_operations_on_outbox_transaction_commit_when_operations_not_successful()
        {
            var fakeContainer = new FakeContainer();
            var fakeCosmosClient = new FakeCosmosClient(fakeContainer);
            var containerHolderHolderResolver = new ContainerHolderResolver(new FakeProvider(fakeCosmosClient),
                new ContainerInformation("fakeContainer", new PartitionKeyPath("/deep/down")), "fakeDatabase");

            var outboxTransaction = new CosmosOutboxTransaction(containerHolderHolderResolver, new ContextBag());
            var storageSession = new CosmosSynchronizedStorageSession(containerHolderHolderResolver);
            await storageSession.TryOpen(outboxTransaction, new ContextBag());

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

            await storageSession.CompleteAsync();

            Assert.That(async () => await outboxTransaction.Commit(), Throws.Exception);
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