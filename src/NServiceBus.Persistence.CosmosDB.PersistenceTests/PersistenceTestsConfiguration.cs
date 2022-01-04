namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Outbox;
    using NServiceBus.Sagas;
    using NUnit.Framework;
    using Persistence;
    using Persistence.CosmosDB;

    public partial class PersistenceTestsConfiguration : IProvideCosmosClient
    {
        static PersistenceTestsConfiguration()
        {
            SagaVariants = new[]
            {
                new TestFixtureData(new TestVariant(new PersistenceConfiguration(usePessimisticLocking: false))).SetArgDisplayNames("Optimistic"),
                new TestFixtureData(new TestVariant(new PersistenceConfiguration(usePessimisticLocking: true))).SetArgDisplayNames("Pessimistic"),
            };

            OutboxVariants = new[]
            {
                new TestFixtureData(new TestVariant(new PersistenceConfiguration(usePessimisticLocking: false))).SetArgDisplayNames("Optimistic"),
            };
        }
        public class PersistenceConfiguration
        {
            public readonly bool UsePessimisticLocking;

            public PersistenceConfiguration(bool usePessimisticLocking)
            {
                UsePessimisticLocking = usePessimisticLocking;
            }
        }


        public bool SupportsDtc => false;

        public bool SupportsOutbox => true;

        public bool SupportsFinders => false;

        public bool SupportsPessimisticConcurrency
        {
            get => false;
            set => throw new NotImplementedException();
        }

        public ISagaIdGenerator SagaIdGenerator { get; } = new SagaIdGenerator();

        public ISagaPersister SagaStorage { get; private set; }

        public ISynchronizedStorage SynchronizedStorage { get; private set; }

        public ISynchronizedStorageAdapter SynchronizedStorageAdapter { get; private set; }

        public IOutboxStorage OutboxStorage { get; private set; }

        public CosmosClient Client { get; } = SetupFixture.CosmosDbClient;

        public int OutboxTimeToLiveInSeconds { get; set; } = 100;

        public Task Configure(CancellationToken cancellationToken = default)
        {
            // with this we have a partition key per run which makes things naturally isolated
            partitionKey = Guid.NewGuid().ToString();

            var serializer = new JsonSerializer
            {
                ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver(),
                Converters = { new ReadOnlyMemoryConverter() }
            };

            var persistenceConfiguration = (PersistenceConfiguration)Variant.Values[0];

            var sagaPersistenceConfiguration = new SagaPersistenceConfiguration();
            if (persistenceConfiguration.UsePessimisticLocking)
            {
                sagaPersistenceConfiguration.UsePessimisticLocking();
            }
            SupportsPessimisticConcurrency = sagaPersistenceConfiguration.PessimisticLockingEnabled;
            if (SessionTimeout.HasValue)
            {
                sagaPersistenceConfiguration.SetPessimisticLeaseLockAcquisitionTimeout(SessionTimeout.Value);
            }

            var partitionKeyPath = new PartitionKeyPath(SetupFixture.PartitionPathKey);
            var resolver = new ContainerHolderResolver(this, new ContainerInformation(SetupFixture.ContainerName, partitionKeyPath), SetupFixture.DatabaseName);
            SynchronizedStorage = new StorageSessionFactory(resolver, null);
            SagaStorage = new SagaPersister(serializer, sagaPersistenceConfiguration);
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

        public Task Cleanup(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        string partitionKey;
    }
}