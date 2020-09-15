namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Container.HasComponent<IProvideCosmosClient>())
            {
                if (context.Settings.TryGet<ClientHolder>(out var clientHolder))
                {
                    context.Container.ConfigureComponent<IProvideCosmosClient>(() => new CosmosClientProvidedByConfiguration { Client = clientHolder.Client }, DependencyLifecycle.SingleInstance);
                }
                else
                {
                    context.Container.ConfigureComponent<IProvideCosmosClient>(() => new ThrowIfNoCosmosClientIsProvided(), DependencyLifecycle.SingleInstance);
                }
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);
            var containerName = context.Settings.Get<string>(SettingsKeys.ContainerName);
            var partitionKeyPath = context.Settings.Get<PartitionKeyPath>();

            context.Container.ConfigureComponent(b => new ContainerHolder(b.Build<IProvideCosmosClient>().Client.GetContainer(databaseName, containerName), partitionKeyPath), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionFactory>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}