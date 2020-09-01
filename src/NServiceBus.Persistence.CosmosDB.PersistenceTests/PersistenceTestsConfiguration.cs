namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
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

        public async Task Configure()
        {
            var connectionStringEnvironmentVariableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(connectionStringEnvironmentVariableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{connectionStringEnvironmentVariableName}' with Cosmos DB connection string.");
            }

            containerName = $"{databaseName}_{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}";
            partitionKey = Guid.NewGuid().ToString();
            var persistenceSettings = new PersistenceExtensions<CosmosDbPersistence>(new SettingsHolder());
            var config = new PartitionAwareConfiguration(persistenceSettings);
            // very big cheat!
            config.AddPartitionMappingForMessageType<object>((headers, id, message) => new PartitionKey(partitionKey), containerName);

            var builder = new CosmosClientBuilder(connectionString);
            builder.AddCustomHandlers(new LoggingHandler());

            cosmosDbClient = builder.Build();

            SynchronizedStorage = new StorageSessionFactory(databaseName, cosmosDbClient, config);

            SagaStorage = new SagaPersister(new JsonSerializerSettings());

            await cosmosDbClient.CreateDatabaseIfNotExistsAsync(databaseName);
            var database = cosmosDbClient.GetDatabase(databaseName);
            // TODO do we need to map PartitionKeyPath as well because it seems we have to.
            await database.CreateContainerAsync(new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = "/partitionKey"
            });

            GetContextBagForSagaStorage = () =>
            {
                var contextBag = new ContextBag();
                // dummy data
                contextBag.Set(new IncomingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Array.Empty<byte>()));
                contextBag.Set(new LogicalMessage(new MessageMetadata(typeof(object)), null));
                return contextBag;
            };
        }

        public async Task Cleanup()
        {
            var database = cosmosDbClient.GetDatabase(databaseName);
            var container = database.GetContainer(containerName);
            await container.DeleteContainerAsync();
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
        string containerName;
        string partitionKey;
    }
}