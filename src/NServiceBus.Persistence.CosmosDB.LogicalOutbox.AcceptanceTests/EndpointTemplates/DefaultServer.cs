namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Cosmos;
using Persistence.CosmosDB;

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
                    transactionInformation.ExtractPartitionKeyFromMessages(new PartitionKeyProvider(runDescriptor.ScenarioContext));
                }

                if (!settings.TryGet<DoNotRegisterDefaultContainerInformationProvider>(out _))
                {
                    transactionInformation.ExtractContainerInformationFromMessage(new ContainerInformationProvider());
                }

                if (settings.TryGet<RegisterFaultyPartitionKeyProvider>(out _))
                {
                    transactionInformation.ExtractPartitionKeyFromMessages(new FaultyPartitionKeyProvider());
                }

                if (settings.TryGet<RegisterFaultyContainerProvider>(out _))
                {
                    transactionInformation.ExtractContainerInformationFromMessage(new FaultyContainerInformationProvider());
                }
            }

            await configurationBuilderCustomization(configuration);
        });

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

    class FaultyPartitionKeyProvider : IPartitionKeyFromMessageExtractor
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