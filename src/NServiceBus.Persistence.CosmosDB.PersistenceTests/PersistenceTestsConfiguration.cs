namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Outbox;
    using NServiceBus.Sagas;
    using Persistence;
    using Persistence.CosmosDB;
    using Persistence.CosmosDB.Outbox;

    public partial class PersistenceTestsConfiguration
    {
        public bool SupportsDtc => false;

        public bool SupportsOutbox => true;

        public bool SupportsFinders => false;

        public bool SupportsPessimisticConcurrency => false;

        public ISagaIdGenerator SagaIdGenerator { get; } = new SagaIdGenerator();

        public ISagaPersister SagaStorage { get; private set; }

        public ISynchronizedStorage SynchronizedStorage { get; private set; }

        public ISynchronizedStorageAdapter SynchronizedStorageAdapter { get; private set; }

        public IOutboxStorage OutboxStorage { get; private set; }

        public Task Configure()
        {
            // with this we have a partition key per run which makes things naturally isolated
            partitionKey = Guid.NewGuid().ToString();

            SynchronizedStorage = new StorageSessionFactory();
            SagaStorage = new SagaPersister(new JsonSerializerSettings());
            OutboxStorage = new OutboxPersister(new JsonSerializerSettings());

            GetContextBagForSagaStorage = () =>
            {
                var contextBag = new ContextBag();
                contextBag.Set(new PartitionKey(partitionKey));
                contextBag.Set(SetupFixture.Container);
                contextBag.Set(ContextBagKeys.PartitionKeyPath, SetupFixture.PartitionPathKey);
                return contextBag;
            };

            GetContextBagForOutbox = () =>
            {
                var contextBag = new ContextBag();
                contextBag.Set(new PartitionKey(partitionKey));
                contextBag.Set(SetupFixture.Container);
                contextBag.Set(ContextBagKeys.PartitionKeyPath, SetupFixture.PartitionPathKey);
                return contextBag;
            };

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }

        string partitionKey;
    }
}