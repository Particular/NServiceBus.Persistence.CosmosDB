namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Microsoft.Azure.Cosmos;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var clientFactory = context.Settings.Get<Func<CosmosClient>>(SettingsKeys.CosmosClient);
            // TODO: Should not be a saga config
            var databaseName = context.Settings.Get<string>(SettingsKeys.Sagas.DatabaseName);
            // TODO: would throw if the extension is not called, make better
            var partitionConfig = context.Settings.Get<PartitionAwareConfiguration>();

            context.Container.ConfigureComponent(() => new StorageSessionFactory(databaseName, clientFactory(), partitionConfig), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}