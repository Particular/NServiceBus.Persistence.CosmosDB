namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;P
    using Logging;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Newtonsoft.Json;
    using NServiceBus.Outbox;
    using NServiceBus.Sagas;
    using Persistence;
    using Persistence.CosmosDB;
    using Settings;

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

        public async Task Configure()
        {
            var connectionStringEnvironmentVariableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(connectionStringEnvironmentVariableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{connectionStringEnvironmentVariableName}' with Cosmos DB connection string.");
            }

            // TODO: Finish
            var routingSettings = new RoutingSettings(new SettingsHolder());
            var config = new PartitionAwareConfiguration(routingSettings);
            // config.MapMessageToContainer()

            SynchronizedStorage = new StorageSessionFactory(databaseName, cosmosDbClient, config);

            var builder = new CosmosClientBuilder(connectionString);
            builder.AddCustomHandlers(new LoggingHandler());

            cosmosDbClient = builder.Build();
            SagaStorage = new SagaPersister(new JsonSerializerSettings());

            await cosmosDbClient.CreateDatabaseIfNotExistsAsync(databaseName);
            //await cosmosDbClient.PopulateContainers(databaseName, SagaMetadataCollection);
        }

        public async Task Cleanup()
        {
            // not really good because it prevents us from running concurrent builds but for now good enough
            // techniqually we could override the convention and prefix things uniquely
            // in addition this might be very slow
            var database = cosmosDbClient.GetDatabase(databaseName);
            foreach (var sagaMetadata in SagaMetadataCollection)
            {
                // TODO: use convention
                var containerName = sagaMetadata.SagaEntityType.Name;

                var container = database.GetContainer(containerName);
                await container.DeleteContainerAsync();
            }
        }

        static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
        }

        class LoggingHandler : RequestHandler
        {
            ILog logger = LogManager.GetLogger<LoggingHandler>();

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode == 429)
                {
                    logger.Info("Request throttled");
                }

                return response;
            }
        }

        const string databaseName = "CosmosDBPersistence";
        CosmosClient cosmosDbClient;
    }
}