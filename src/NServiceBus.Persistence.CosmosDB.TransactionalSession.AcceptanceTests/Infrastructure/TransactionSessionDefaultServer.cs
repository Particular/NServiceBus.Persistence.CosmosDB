namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Persistence.CosmosDB;

    public class TransactionSessionDefaultServer : IEndpointSetupTemplate
    {
        public virtual Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.EnableInstallers();

            builder.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));
            builder.SendFailedMessagesTo("error");

            var storageDir = Path.Combine(Path.GetTempPath(), "learn", TestContext.CurrentContext.Test.ID);

            var transport = builder.UseTransport<AcceptanceTestingTransport>();
            transport.StorageDirectory(storageDir);

            var persistence = builder.UsePersistence<CosmosPersistence>();
            persistence.EnableTransactionalSession();
            persistence.DisableContainerCreation();
            persistence.CosmosClient(SetupFixture.CosmosDbClient);
            persistence.DatabaseName(SetupFixture.DatabaseName);

            persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

            builder.RegisterComponents(services => services.ConfigureComponent<PartitionKeyProvider>(DependencyLifecycle.SingleInstance));

            builder.RegisterComponents(c => c.RegisterSingleton(runDescriptor.ScenarioContext)); // register base ScenarioContext type
            builder.RegisterComponents(c => c.RegisterSingleton(runDescriptor.ScenarioContext.GetType(), runDescriptor.ScenarioContext)); // register specific implementation

            endpointConfiguration.TypesToInclude.Add(typeof(CaptureBuilderFeature)); // required because the test assembly is excluded from scanning by default
            builder.EnableFeature<CaptureBuilderFeature>();

            configurationBuilderCustomization(builder);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            builder.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());

            return Task.FromResult(builder);
        }

        class PartitionKeyProvider : IPartitionKeyFromHeadersExtractor
        {

            public PartitionKeyProvider(ScenarioContext scenarioContext) => this.scenarioContext = scenarioContext;

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
                return true;
            }

            readonly ScenarioContext scenarioContext;
        }
    }
}