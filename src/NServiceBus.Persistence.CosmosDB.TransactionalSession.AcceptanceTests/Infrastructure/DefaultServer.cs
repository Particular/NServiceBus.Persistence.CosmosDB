namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.CosmosDB;

public class DefaultServer : IEndpointSetupTemplate
{
    public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomization,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointCustomization.EndpointName);

        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        endpointConfiguration.Recoverability()
            .Delayed(delayed => delayed.NumberOfRetries(0))
            .Immediate(immediate => immediate.NumberOfRetries(0));
        endpointConfiguration.SendFailedMessagesTo("error");

        string storageDir = Path.Combine(Path.GetTempPath(), "learn", TestContext.CurrentContext.Test.ID);

        endpointConfiguration.UseTransport(new AcceptanceTestingTransport { StorageLocation = storageDir });

        var persistence = endpointConfiguration.UsePersistence<CosmosPersistence>();

        endpointConfiguration.GetSettings().Set(persistence);
        persistence.EnableTransactionalSession();
        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

        endpointConfiguration.RegisterComponents(services => services.AddSingleton<IPartitionKeyFromHeadersExtractor, PartitionKeyProvider>());

        if (runDescriptor.ScenarioContext is TransactionalSessionTestContext testContext)
        {
            endpointConfiguration.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, testContext, endpointCustomization.EndpointName));
        }

        await configurationBuilderCustomization(endpointConfiguration);

        // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
        endpointConfiguration.TypesToIncludeInScan(endpointCustomization.GetTypesScopedByTestClass());

        return endpointConfiguration;
    }

    class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
            return true;
        }
    }
}