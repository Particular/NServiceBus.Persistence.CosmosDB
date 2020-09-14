namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Microsoft.Azure.Cosmos;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<ClientHolder>(SettingsKeys.CosmosClient).Client;
            var resolveFromContainer = context.Settings.HasExplicitValue(SettingsKeys.ResolveClientFromContainer);

            if (client is null && resolveFromContainer == false)
            {
                throw new Exception("You must configure a CosmosClient, provide a connection string or configure to resolve if from the container.");
            }

            if (resolveFromContainer)
            {
                context.Container.ConfigureComponent(b => new ClientHolder { Client = b.Build<CosmosClient>() }, DependencyLifecycle.SingleInstance);
            }
            else
            {
                context.Container.ConfigureComponent(b => new ClientHolder { Client = client }, DependencyLifecycle.SingleInstance);
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);
            var containerName = context.Settings.Get<string>(SettingsKeys.ContainerName);
            var partitionKeyPath = context.Settings.Get<PartitionKeyPath>();

            context.Container.ConfigureComponent(b => new ContainerHolder(b.Build<ClientHolder>().Client.GetContainer(databaseName, containerName), partitionKeyPath), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionFactory>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}