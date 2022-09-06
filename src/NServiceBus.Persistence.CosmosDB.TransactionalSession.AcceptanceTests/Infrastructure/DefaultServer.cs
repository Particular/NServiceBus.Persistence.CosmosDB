namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
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

            persistence.DisableContainerCreation();
            persistence.CosmosClient(SetupFixture.CosmosDbClient);
            persistence.DatabaseName(SetupFixture.DatabaseName);

            persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

            await configurationBuilderCustomization(builder).ConfigureAwait(false);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            builder.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());

            return builder;
        }
    }
}