﻿using System;
using System.Threading.Tasks;
using NServiceBus.Gateway.Deduplication;
using NServiceBus.Outbox;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Sagas;

using NServiceBus.Timeout.Core;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

namespace NServiceBus.Persistence.ComponentTests
{
    using System.Net;
    using System.Threading;
    using Features;
    using Logging;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;

    public partial class PersistenceTestsConfiguration
    {
      
        public bool SupportsDtc { get; } = false;

        public bool SupportsOutbox { get; } = false;

        public bool SupportsFinders { get; } = false;

        public bool SupportsSubscriptions { get; } = true;

        public bool SupportsTimeouts { get; } = false;

        public ISagaIdGenerator SagaIdGenerator { get; } = new SagaIdGenerator();

        public ISagaPersister SagaStorage { get; internal set;  }

        public ISynchronizedStorage SynchronizedStorage { get; internal set; }

        public ISynchronizedStorageAdapter SynchronizedStorageAdapter { get; }

        public ISubscriptionStorage SubscriptionStorage { get; set; }

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

            var builder = new CosmosClientBuilder(connectionString);
            builder.AddCustomHandlers(new LoggingHandler());

            var cosmosClient = builder.Build();

            SagaStorage = new SagaPersister(cosmosClient, databaseName, containerName);

            SubscriptionStorage = new SubscriptionPersister(cosmosClient, databaseName, "Subscriptions");

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

    class LoggingHandler : RequestHandler
    {
        ILog logger = LogManager.GetLogger<LoggingHandler>();

        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.Info("Request throttled");
            }

            return response;
        }
    }
}