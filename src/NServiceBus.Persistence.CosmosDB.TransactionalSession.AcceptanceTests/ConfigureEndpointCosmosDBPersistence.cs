using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.TransactionalSession.AcceptanceTests;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<CosmosPersistence>();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        configuration.RegisterComponents(services => services.AddSingleton<IPartitionKeyFromHeadersExtractor, PartitionKeyProvider>());
        configuration.RegisterComponents(services =>
            services.AddSingleton<IContainerInformationFromHeadersExtractor, ContainerInformationProvider>());

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;

    class PartitionKeyProvider : IPartitionKeyFromHeadersExtractor
    {
        readonly ScenarioContext scenarioContext;

        public PartitionKeyProvider(ScenarioContext scenarioContext) => this.scenarioContext = scenarioContext;

        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            return true;
        }
    }

    class ContainerInformationProvider : IContainerInformationFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
            return true;
        }
    }
}