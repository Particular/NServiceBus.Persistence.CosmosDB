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
            return Task.CompletedTask;
        }

        PersistenceExtensions<CosmosPersistence> persistence = configuration.UsePersistence<CosmosPersistence>();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        if (endpointName.StartsWith("SubscribersHandlesTheSameEvent"))
        {
            //NOTE this call is required to ensure that the default synthetic partition key is used. The override uses the TestRunId as the partition key which will cause this test to fail
            settings.DoNotRegisterDefaultPartitionKeyProvider();
        }

        if (!settings.TryGet<DoNotRegisterDefaultPartitionKeyProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IPartitionKeyFromMessageExtractor, PartitionKeyProvider>());
        }

        if (!settings.TryGet<DoNotRegisterDefaultContainerInformationProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IContainerInformationFromMessagesExtractor, ContainerInformationProvider>());
        }

        if (settings.TryGet<RegisterFaultyPartitionKeyProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IPartitionKeyFromMessageExtractor, FaultyPartitionKeyProvider>());
        }

        if (settings.TryGet<RegisterFaultyContainerProvider>(out _))
        {
            configuration.RegisterComponents(services => services.AddSingleton<IContainerInformationFromMessagesExtractor, FaultyContainerInformationProvider>());
        }

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;

    class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromMessageExtractor
    {
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

    class FaultyPartitionKeyProvider() : IPartitionKeyFromMessageExtractor
    {
        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = null;
            return false;
        }
    }

    class FaultyContainerInformationProvider : IContainerInformationFromMessagesExtractor
    {
        public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            containerInformation = null;
            return false;
        }
    }
}