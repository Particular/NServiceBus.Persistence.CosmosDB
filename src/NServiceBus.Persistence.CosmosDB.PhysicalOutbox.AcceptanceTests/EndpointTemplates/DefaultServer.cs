namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System;
using System.Threading.Tasks;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;

public class DefaultServer : ServerWithNoDefaultPersistenceDefinitions
{
    public override Task<EndpointConfiguration> GetConfiguration(
        RunDescriptor runDescriptor,
        EndpointCustomizationConfiguration endpointCustomizationConfiguration,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
        base.GetConfiguration(runDescriptor, endpointCustomizationConfiguration, async configuration =>
        {
            await configuration.DefinePersistence(runDescriptor, endpointCustomizationConfiguration);

            if (!configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
            {
                var settings = runDescriptor.Settings;
                var transactionInformation = configuration.UsePersistence<CosmosPersistence>().TransactionInformation();

                if (!settings.TryGet<DoNotRegisterDefaultPartitionKeyProvider>(out _))
                {
                    transactionInformation.ExtractPartitionKeyFromHeaders(new PartitionKeyProvider(runDescriptor.ScenarioContext));
                }

                if (!settings.TryGet<DoNotRegisterDefaultContainerInformationProvider>(out _))
                {
                    transactionInformation.ExtractContainerInformationFromHeaders(new ContainerInformationProvider());
                }

                if (settings.TryGet<RegisterFaultyPartitionKeyProvider>(out _))
                {
                    transactionInformation.ExtractPartitionKeyFromHeaders(new FaultyPartitionKeyProvider());
                }

                if (settings.TryGet<RegisterFaultyContainerProvider>(out _))
                {
                    transactionInformation.ExtractContainerInformationFromHeaders(new FaultyContainerInformationProvider());
                }
            }

            await configurationBuilderCustomization(configuration);
        });
}