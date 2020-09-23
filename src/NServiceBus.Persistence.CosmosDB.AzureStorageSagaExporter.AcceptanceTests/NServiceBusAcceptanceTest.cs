namespace NServiceBus.Persistence.CosmosDB.AzureStorageSagaExporter.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using NUnit.Framework;

    [TestFixture]
    public abstract class NServiceBusAcceptanceTest
    {
        [SetUp]
        public virtual Task SetUp()
        {
            NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var testName = classAndEndpoint.Split('+').First();

                testName = testName.Replace("When_", "");

                var endpointBuilder = classAndEndpoint.Split('+').Last();


                testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

                testName = testName.Replace("_", "");

                return testName + "." + endpointBuilder;
            };

            return Task.CompletedTask;
        }

        [TearDown]
        public virtual Task TearDown()
        {
            return Task.CompletedTask;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var connectionStringEnvironmentVariableName = "CosmosDBPersistence_ConnectionString";
            var connectionString = GetEnvironmentVariable(connectionStringEnvironmentVariableName,
                fallbackEmulatorConnectionString: "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

            var builder = new CosmosClientBuilder(connectionString);

            CosmosClient = builder.Build();

            var database = CosmosClient.GetDatabase(DatabaseName);

            await CosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);

            ContainerName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}";

            var containerProperties = new ContainerProperties(ContainerName, PartitionPathKey);

            await database.CreateContainerIfNotExistsAsync(containerProperties)
                .ConfigureAwait(false);

            var environmentVariableName = "AzureStoragePersistence_ConnectionString";
            AzureStoragePersistenceConnectionString = GetEnvironmentVariable(environmentVariableName, fallbackEmulatorConnectionString: "UseDevelopmentStorage=true");

            Container = database.GetContainer(ContainerName);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await Container.DeleteContainerAsync();
        }

        static string GetEnvironmentVariable(string variable, string fallbackEmulatorConnectionString)
        {
            var candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);
            var environmentVariableConnectionString = string.IsNullOrWhiteSpace(candidate) ? Environment.GetEnvironmentVariable(variable) : candidate;

            return string.IsNullOrEmpty(environmentVariableConnectionString) ? fallbackEmulatorConnectionString : environmentVariableConnectionString;
        }

        protected CosmosClient CosmosClient { get; set; }
        protected string AzureStoragePersistenceConnectionString { get; set; }
        protected const string DatabaseName = "CosmosDBPersistence";
        protected const string PartitionPathKey = "/id";
        protected static string ContainerName;
        private Container Container;
    }
}
