namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NServiceBus.Outbox;
    using NServiceBus.Sagas;
    using Persistence;
    using Persistence.CosmosDB;
    using Pipeline;
    using Settings;
    using Transport;
    using Unicast.Messages;

    public partial class PersistenceTestsConfiguration
    {
        public bool SupportsDtc => false;

        public bool SupportsOutbox => false;

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

            var persistenceSettings = new PersistenceExtensions<CosmosDbPersistence>(new SettingsHolder());
            var config = new PartitionAwareConfiguration(persistenceSettings);
            // very big cheat!
            config.AddPartitionMappingForMessageType<object>((headers,
                    id,
                    message) => new PartitionKey(partitionKey),
                SetupFixture.ContainerName,
                SetupFixture.PartitionPathKey);

            SynchronizedStorage = new StorageSessionFactory(SetupFixture.DatabaseName, SetupFixture.cosmosDbClient, config);
            SagaStorage = new SagaPersister(new JsonSerializerSettings());

            GetContextBagForSagaStorage = () =>
            {
                var contextBag = new ContextBag();
                // dummy data
                contextBag.Set(new IncomingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Array.Empty<byte>()));
                contextBag.Set(new LogicalMessage(new MessageMetadata(typeof(object)), null));
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