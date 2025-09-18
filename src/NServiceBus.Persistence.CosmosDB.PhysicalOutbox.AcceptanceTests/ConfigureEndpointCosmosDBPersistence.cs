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

        PersistenceExtensions<CosmosPersistence> persistence = configuration.UsePersistence<CosmosPersistence>();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        if (!settings.TryGet<DoNotRegisterDefaultPartitionKeyProvider>(out _))
        {
            configuration.RegisterComponents(services =>
                services.AddSingleton<IPartitionKeyFromHeadersExtractor>(provider =>
                    new PartitionKeyProvider(provider.GetRequiredService<ScenarioContext>(), endpointName)
                )
            );
        }

        if (!settings.TryGet<DoNotRegisterDefaultContainerInformationProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IContainerInformationFromHeadersExtractor, ContainerInformationProvider>());
        }

        return Task.FromResult(0);
    }

    public Task Cleanup() => Task.CompletedTask;

    class PartitionKeyProvider(ScenarioContext scenarioContext, string endpointName) : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey($"{endpointName}-{headers["NServiceBus.MessageId"]}");
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