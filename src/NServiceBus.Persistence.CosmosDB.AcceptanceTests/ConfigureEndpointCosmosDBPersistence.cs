using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.Configuration.AdvancedExtensibility;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        if (configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
        {
            return Task.FromResult(0);
        }

        configuration.GetSettings().Set(SetupFixture.config);

        var persistence = configuration.UsePersistence<CosmosDbPersistence>();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        return Task.CompletedTask;
    }
}