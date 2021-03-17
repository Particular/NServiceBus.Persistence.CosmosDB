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

    public partial class PersistenceTestsConfiguration : IProvideCosmosClient
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

        public CosmosClient Client { get; } = SetupFixture.CosmosDbClient;

        public int OutboxTimeToLiveInSeconds { get; set; } = 100;

        public Task Configure()
        {
            // with this we have a partition key per run which makes things naturally isolated
            partitionKey = Guid.NewGuid().ToString();

            var serializer = new JsonSerializer
            {
                ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver()
            };

            var partitionKeyPath = new PartitionKeyPath(SetupFixture.PartitionPathKey);
            var resolver = new ContainerHolderResolver(this, new ContainerInformation(SetupFixture.ContainerName, partitionKeyPath), SetupFixture.DatabaseName);
            SynchronizedStorage = new StorageSessionFactory(resolver, null);
            SagaStorage = new SagaPersister(serializer, false);
            OutboxStorage = new OutboxPersister(resolver, serializer, OutboxTimeToLiveInSeconds);

            GetContextBagForSagaStorage = () =>
            {
                var contextBag = new ContextBag();
                // This populates the partition key required to participate in a shared transaction
                var setAsDispatchedHolder = new SetAsDispatchedHolder
                {
                    PartitionKey = new PartitionKey(partitionKey),
                    ContainerHolder = resolver.ResolveAndSetIfAvailable(contextBag)
                };
                contextBag.Set(setAsDispatchedHolder);
                contextBag.Set(new PartitionKey(partitionKey));
                return contextBag;
            };

            GetContextBagForOutbox = () =>
            {
                var contextBag = new ContextBag();
                // This populates the partition key required to participate in a shared transaction
                var setAsDispatchedHolder = new SetAsDispatchedHolder
                {
                    PartitionKey = new PartitionKey(partitionKey),
                    ContainerHolder = resolver.ResolveAndSetIfAvailable(contextBag)
                };
                contextBag.Set(setAsDispatchedHolder);
                contextBag.Set(new PartitionKey(partitionKey));
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