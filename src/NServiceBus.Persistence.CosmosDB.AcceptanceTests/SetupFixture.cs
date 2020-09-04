namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Sagas;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using NServiceBus.Sagas;
    using NUnit.Framework;
    using Persistence.CosmosDB;
    using Settings;

    [SetUpFixture]
    public class SetupFixture
    {
        public const string DatabaseName = "CosmosDBPersistence";
        public const string PartitionPathKey = "/deep/down";
        public static string ContainerName;
        public static CosmosClient CosmosDbClient;
        public static Container Container;
        public static PartitionAwareConfiguration config;
        static double totalRequestCharges = 0;
        SagaMetadataCollection sagaMetadataCollection;

        public SagaMetadataCollection SagaMetadataCollection
        {
            get
            {
                if (sagaMetadataCollection == null)
                {
                    var sagaTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(Saga).IsAssignableFrom(t) || typeof(IFindSagas<>).IsAssignableFrom(t) || typeof(IFinder).IsAssignableFrom(t)).ToArray();
                    sagaMetadataCollection = new SagaMetadataCollection();
                    sagaMetadataCollection.Initialize(sagaTypes);
                }

                return sagaMetadataCollection;
            }
            set { sagaMetadataCollection = value; }
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var connectionStringEnvironmentVariableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(connectionStringEnvironmentVariableName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Oh no! We couldn't find an environment variable '{connectionStringEnvironmentVariableName}' with Cosmos DB connection string.");
            }

            ContainerName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}";

            var builder = new CosmosClientBuilder(connectionString);
            builder.AddCustomHandlers(new LoggingHandler());

            CosmosDbClient = builder.Build();

            var persistenceSettings = new PersistenceExtensions<CosmosDbPersistence>(new SettingsHolder());
            config = new PartitionAwareConfiguration(persistenceSettings);
            // we actually don't really care about the mapping function on this level, we just need the path
            config.AddPartitionMappingForMessageType<When_message_has_a_saga_id.MessageWithSagaId>((h, id, m)=> new PartitionKey(m.DataId.ToString()), SetupFixture.ContainerName, "/partitionKey");
            config.AddPartitionMappingForMessageType<When_handling_concurrent_messages.StartMsg>((h, id, m)=> new PartitionKey(m.OrderId), SetupFixture.ContainerName, "/partitionKey");
            config.AddPartitionMappingForMessageType<When_handling_concurrent_messages.ContinueMsg>((h, id, m)=> new PartitionKey(m.OrderId), SetupFixture.ContainerName, "/partitionKey");
            config.AddPartitionMappingForMessageType<When_handling_concurrent_messages.FinishMsg>((h, id, m)=> new PartitionKey(m.OrderId), SetupFixture.ContainerName, "/partitionKey");

            await CosmosDbClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
            await CosmosDbClient.PopulateContainers(DatabaseName, SagaMetadataCollection, config);

            var database = CosmosDbClient.GetDatabase(DatabaseName);
            Container = database.GetContainer(ContainerName);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await Container.DeleteContainerStreamAsync();
            CosmosDbClient.Dispose();
        }

        static string GetEnvironmentVariable(string variable)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;
        }

        class LoggingHandler : RequestHandler
        {
            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                var requestCharge = response.Headers["x-ms-request-charge"];
                await TestContext.Progress.WriteLineAsync($"Charged RUs:{requestCharge} for {request.Method.Method} {request.RequestUri} IsBatch:{request.Headers["x-ms-cosmos-is-batch-request"]}");
                totalRequestCharges += Convert.ToDouble(requestCharge);

                await TestContext.Progress.WriteLineAsync($"Total charged RUs: {totalRequestCharges}");

                if ((int)response.StatusCode == 429)
                {
                    await TestContext.Progress.WriteLineAsync("Request throttled.");
                }

                return response;
            }
        }
    }
}