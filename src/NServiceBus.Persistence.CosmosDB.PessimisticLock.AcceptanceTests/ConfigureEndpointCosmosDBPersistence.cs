using System.Threading.Tasks;
using NServiceBus;
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

        persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

        SagaPersistenceConfiguration sagasConfiguration = persistence.Sagas();
        sagasConfiguration.UsePessimisticLocking();

        return Task.FromResult(0);
    }

    public Task Cleanup() => Task.CompletedTask;
}