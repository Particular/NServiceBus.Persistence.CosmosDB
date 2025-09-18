namespace NServiceBus.PersistenceTesting;

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
        SagaVariants = [new TestFixtureData(new TestVariant(new PersistenceConfiguration(false))).SetArgDisplayNames("Optimistic"), new TestFixtureData(new TestVariant(new PersistenceConfiguration(true))).SetArgDisplayNames("Pessimistic")];

        OutboxVariants = [new TestFixtureData(new TestVariant(new PersistenceConfiguration(false))).SetArgDisplayNames("Optimistic")];
    }

    public class PersistenceConfiguration(bool usePessimisticLocking)
    {
        public readonly bool UsePessimisticLocking = usePessimisticLocking;
    }


    public bool SupportsDtc => false;

    public bool SupportsOutbox => true;

    public bool SupportsFinders => false;

    public bool SupportsPessimisticConcurrency { get; private set; }

    public ISagaIdGenerator SagaIdGenerator { get; } = new SagaIdGenerator();

    public ISagaPersister SagaStorage { get; private set; }

    public IOutboxStorage OutboxStorage { get; private set; }

    public CosmosClient Client { get; } = SetupFixture.CosmosDbClient;

    public Func<ICompletableSynchronizedStorageSession> CreateStorageSession { get; private set; }

    public int OutboxTimeToLiveInSeconds { get; set; } = 100;

    public Task Configure(CancellationToken cancellationToken = default)
    {
        // with this we have a partition key per run which makes things naturally isolated
        // setting this essentialy means overriding the default synthetic partition key strategy
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
            PessimisticLockingConfiguration pessimisticLockingConfiguration = sagaPersistenceConfiguration.UsePessimisticLocking();
            if (SessionTimeout.HasValue)
            {
                pessimisticLockingConfiguration.SetLeaseLockAcquisitionTimeout(SessionTimeout.Value);
            }

            SupportsPessimisticConcurrency = true;
        }

        var partitionKeyPath = new PartitionKeyPath(SetupFixture.PartitionPathKey);
        var resolver = new ContainerHolderResolver(this, new ContainerInformation(SetupFixture.ContainerName, partitionKeyPath), SetupFixture.DatabaseName);
        SagaStorage = new SagaPersister(serializer, sagaPersistenceConfiguration);
        OutboxStorage = new OutboxPersister(resolver, serializer, "SomeProcessingEndpoint", true, new ExtractorConfiguration { HasCustomPartitionHeaderExtractors = true }, OutboxTimeToLiveInSeconds);

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

        CreateStorageSession = () =>
        {
            return new CosmosSynchronizedStorageSession(resolver);
        };

        return Task.CompletedTask;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;

    string partitionKey;
}