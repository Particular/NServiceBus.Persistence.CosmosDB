using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Pipeline;

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

        configuration.RegisterComponents(services => services.AddSingleton<IExtractTransactionInformationFromMessages, PartitionKeyProvider>());

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }

    class PartitionKeyProvider : IExtractTransactionInformationFromMessages
    {
        ScenarioContext scenarioContext;

        public PartitionKeyProvider(ScenarioContext scenarioContext) => this.scenarioContext = scenarioContext;

        public bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
            return true;
        }
    }
}
