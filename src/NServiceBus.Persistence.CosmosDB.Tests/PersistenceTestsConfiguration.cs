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
      
        public bool SupportsDtc { get; } = false;

        public bool SupportsOutbox { get; } = false;

        public bool SupportsFinders { get; } = false;

        public bool SupportsSubscriptions { get; } = false;

        public bool SupportsTimeouts { get; } = false;

        public ISagaIdGenerator SagaIdGenerator { get; } = new SagaIdGenerator();

        public ISagaPersister SagaStorage { get; internal set;  }

        public ISynchronizedStorage SynchronizedStorage { get; internal set; }

        public ISynchronizedStorageAdapter SynchronizedStorageAdapter { get; }

        public ISubscriptionStorage SubscriptionStorage { get; }

        public IPersistTimeouts TimeoutStorage { get; }

        public IQueryTimeouts TimeoutQuery { get; }

        public IOutboxStorage OutboxStorage { get; }

        public IDeduplicateMessages GatewayStorage { get; }

        public Task Configure()
        {
            var connectionStringEnvironmentVartiableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(connectionStringEnvironmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{connectionStringEnvironmentVartiableName}' with Cosmos DB connection string.");
            }

            var databaseEnvironmentVartiableName = "CosmosDBPersistence_DatabaseName";
            var databaseName = GetEnvironmentVariable(databaseEnvironmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{databaseEnvironmentVartiableName}' with Cosmos DB database name.");
            }

            var containerEnvironmentVartiableName = "CosmosDBPersistence_ContainerName";
            var containerName = GetEnvironmentVariable(containerEnvironmentVartiableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{containerEnvironmentVartiableName}' with Cosmos DB container name.");
            }

            SynchronizedStorage = new SynchronizedStorageForTesting();

            SagaStorage = new SagaPersister(new CosmosClient(connectionString), databaseName, containerName);


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