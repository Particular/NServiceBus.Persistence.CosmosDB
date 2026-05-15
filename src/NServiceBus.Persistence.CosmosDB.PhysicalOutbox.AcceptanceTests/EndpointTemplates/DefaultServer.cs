namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Cosmos;
using Outbox;
using Persistence.CosmosDB;

public class DefaultServer : ServerWithNoDefaultPersistenceDefinitions
{
    public override Task<EndpointConfiguration> GetConfiguration(
        RunDescriptor runDescriptor,
        EndpointCustomizationConfiguration endpointCustomizationConfiguration,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
        base.GetConfiguration(runDescriptor, endpointCustomizationConfiguration, async configuration =>
        {
            if (!configuration.GetSettings().Get<bool>("Endpoint.SendOnly"))
            {
                var settings = runDescriptor.Settings;
                var endpointName = endpointCustomizationConfiguration.EndpointName;
                var transactionInformation = configuration.UsePersistence<CosmosPersistence>().TransactionInformation();

                if (endpointName.StartsWith(Conventions.EndpointNamingConvention(typeof(When_outbox_is_used_by_multiple_subscribers_for_the_same_event.Publisher)).Split('.')[0]))
                {
                    //NOTE this call is required to ensure that the default synthetic partition key is used. The override uses the TestRunId as the partition key which will cause this test to fail
                    settings.DoNotRegisterDefaultPartitionKeyProvider();
                }

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

    class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
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

    class FaultyPartitionKeyProvider : IPartitionKeyFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
        {
            partitionKey = null;
            return false;
        }
    }

    class FaultyContainerInformationProvider : IContainerInformationFromHeadersExtractor
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            containerInformation = null;
            return false;
        }
    }
}