namespace AzureStoragePersistenceSagaExporter.AcceptanceTests
{
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using System;
    using System.Threading.Tasks;

    public class BaseEndpoint : IEndpointSetupTemplate
    {
        public virtual Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var endpointConfiguration = new EndpointConfiguration(endpointCustomizationConfiguration.EndpointName);

            endpointConfiguration.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));

            endpointConfiguration.UseTransport<AcceptanceTestingTransport>();

            endpointConfiguration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            configurationBuilderCustomization(endpointConfiguration);

            return Task.FromResult(endpointConfiguration);
        }
    }
}
