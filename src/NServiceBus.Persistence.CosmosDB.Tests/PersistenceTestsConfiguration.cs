using System;
using System.Globalization;
using System.Threading.Tasks;
using NServiceBus.Gateway.Deduplication;
using NServiceBus.Outbox;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Sagas;

using NServiceBus.Timeout.Core;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

namespace NServiceBus.Persistence.ComponentTests
{
    using Microsoft.Azure.Cosmos;

    public partial class PersistenceTestsConfiguration
    {
        public string DatabaseName { get; }

        public Func<Type, string> CollectionNamingConvention { get; }

        public PersistenceTestsConfiguration(string versionElementName, Func<Type, string> collectionNamingConvention)
        {
            DatabaseName = "Test_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
            CollectionNamingConvention = collectionNamingConvention;

            SagaIdGenerator = new SagaIdGenerator();
           
        }

        public PersistenceTestsConfiguration() : this("_version", t => t.Name.ToLower())
        {
        }

        public PersistenceTestsConfiguration(string versionElementName) : this(versionElementName, t => t.Name.ToLower())
        {
        }

        public bool SupportsDtc { get; } = false;

        public bool SupportsOutbox { get; } = false;

        public bool SupportsFinders { get; } = false;

        public bool SupportsSubscriptions { get; } = false;

        public bool SupportsTimeouts { get; } = false;

        public ISagaIdGenerator SagaIdGenerator { get; }

        public ISagaPersister SagaStorage { get; internal set;  }

        public ISynchronizedStorage SynchronizedStorage { get; }

        public ISynchronizedStorageAdapter SynchronizedStorageAdapter { get; }

        public ISubscriptionStorage SubscriptionStorage { get; }

        public IPersistTimeouts TimeoutStorage { get; }

        public IQueryTimeouts TimeoutQuery { get; }

        public IOutboxStorage OutboxStorage { get; }

        public IDeduplicateMessages GatewayStorage { get; }

        public Task Configure()
        {
            var environmentVartiableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(environmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{environmentVartiableName}' with Cosmos DB connection string.");
            }

            SagaStorage = new SagaPersister(new CosmosClient(connectionString));

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {

            return Task.FromResult(0);
        }

        public Task CleanupMessagesOlderThan(DateTimeOffset beforeStore)
        {
            return Task.FromResult(0);
        }

        static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
        }
    }
}