﻿namespace NServiceBus.Persistence.CosmosDB.AzureStorageSagaExporter.AcceptanceTests
{
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using ObjectBuilder;

    public static class EndpointConfigurationExtensions
    {
        public static void RegisterComponentsAndInheritanceHierarchy(this EndpointConfiguration builder, RunDescriptor runDescriptor)
        {
            builder.RegisterComponents(r => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, r); });
        }

        static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor, IConfigureComponents r)
        {
            var type = runDescriptor.ScenarioContext.GetType();
            while (type != typeof(object))
            {
                r.RegisterSingleton(type, runDescriptor.ScenarioContext);
                type = type.BaseType;
            }
        }

        public static TransportExtensions ConfigureTransport(this EndpointConfiguration endpointConfiguration)
        {
            return new TransportExtensions(endpointConfiguration.GetSettings());
        }
    }
}
