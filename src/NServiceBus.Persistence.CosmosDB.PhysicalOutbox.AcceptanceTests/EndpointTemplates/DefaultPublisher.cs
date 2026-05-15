namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System;
using System.Threading.Tasks;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;

public class DefaultPublisher : IEndpointSetupTemplate
{
    public Task<EndpointConfiguration> GetConfiguration(
        RunDescriptor runDescriptor,
        EndpointCustomizationConfiguration endpointCustomizationConfiguration,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization)
    {
        runDescriptor.Settings.DoNotRegisterDefaultPartitionKeyProvider();
        return new DefaultServer().GetConfiguration(runDescriptor, endpointCustomizationConfiguration, configurationBuilderCustomization);
    }
}