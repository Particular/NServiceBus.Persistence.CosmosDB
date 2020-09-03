namespace NServiceBus.PersistenceTesting
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using NUnit.Framework;
    using Persistence.CosmosDB;
    using Settings;

    [SetUpFixture]
    public class SetupFixture
    {
        public const string DatabaseName = "CosmosDBPersistence";
        public const string PartitionPathKey = "/deep/down";
        public static string ContainerName;
        public static CosmosClient cosmosDbClient;
        static double totalRequestCharges = 0;

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

            cosmosDbClient = builder.Build();

            // should we ever need the test variant we have a problem
            var persistenceTestConfiguration = new PersistenceTestsConfiguration(new TestVariant("default"));

            var persistenceSettings = new PersistenceExtensions<CosmosDbPersistence>(new SettingsHolder());
            var config = new PartitionAwareConfiguration(persistenceSettings);
            // we actually don't really care about the mapping function on this level, we just need the path
            config.AddPartitionMappingForMessageType<object>((headers,
                    id,
                    message) => new PartitionKey("partitionKey"),
                SetupFixture.ContainerName,
                SetupFixture.PartitionPathKey);

            var maxThroughput = ThroughputProperties.CreateAutoscaleThroughput(4_000);

            await cosmosDbClient.CreateDatabaseIfNotExistsAsync(DatabaseName, maxThroughput);
            await cosmosDbClient.PopulateContainers(DatabaseName, persistenceTestConfiguration.SagaMetadataCollection, config, cheat: true);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            var database = cosmosDbClient.GetDatabase(DatabaseName);
            var container = database.GetContainer(ContainerName);
            await container.DeleteContainerStreamAsync();
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