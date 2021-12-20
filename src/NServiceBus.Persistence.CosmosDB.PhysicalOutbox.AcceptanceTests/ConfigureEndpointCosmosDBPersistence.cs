using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Persistence.CosmosDB;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        if (configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
        {
            return Task.FromResult(0);
        }

        var persistence = configuration.UsePersistence<CosmosPersistence>();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        // This populates the partition key at the physical stage to test the conventional outbox use-case
        configuration.RegisterComponents(services => services.AddSingleton<IExtractTransactionInformationFromHeaders, PartitionKeyProvider>());

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }

    class PartitionKeyProvider : IExtractTransactionInformationFromHeaders
    {
        ScenarioContext scenarioContext;

        public PartitionKeyProvider(ScenarioContext scenarioContext) => this.scenarioContext = scenarioContext;

        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
            out ContainerInformation? containerInformation)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
            return true;
        }
    }
}