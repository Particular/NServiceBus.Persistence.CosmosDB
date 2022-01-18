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

        if (!settings.TryGet<DoNotRegisterDefaultPartitionKeyProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IPartitionKeyFromMessageExtractor, PartitionKeyProvider>());
        }
        if (!settings.TryGet<DoNotRegisterDefaultContainerInformationProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IContainerInformationFromMessagesExtractor, ContainerInformationProvider>());
        }

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }

    class PartitionKeyProvider : IPartitionKeyFromMessageExtractor
    {
        readonly ScenarioContext scenarioContext;

        public PartitionKeyProvider(ScenarioContext scenarioContext) => this.scenarioContext = scenarioContext;

        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            return true;
        }
    }

    class ContainerInformationProvider : IContainerInformationFromMessagesExtractor
    {
        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
            return true;
        }
    }
}
