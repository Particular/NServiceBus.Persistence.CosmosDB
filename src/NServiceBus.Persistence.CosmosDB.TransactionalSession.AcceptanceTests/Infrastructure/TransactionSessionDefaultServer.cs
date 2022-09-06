namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using NUnit.Framework;

    public class TransactionSessionDefaultServer : IEndpointSetupTemplate
    {
        public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.EnableInstallers();

            builder.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));
            builder.SendFailedMessagesTo("error");

            var storageDir = Path.Combine(Path.GetTempPath(), "learn", TestContext.CurrentContext.Test.ID);

            builder.UseTransport(new AcceptanceTestingTransport
            {
                StorageLocation = storageDir
            });

            var persistence = builder.UsePersistence<CosmosPersistence>();
            persistence.EnableTransactionalSession();
            persistence.DisableContainerCreation();
            persistence.CosmosClient(SetupFixture.CosmosDbClient);
            persistence.DatabaseName(SetupFixture.DatabaseName);

            persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

            builder.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, runDescriptor.ScenarioContext));

            await configurationBuilderCustomization(builder).ConfigureAwait(false);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            builder.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());

            return builder;
        }
    }
}