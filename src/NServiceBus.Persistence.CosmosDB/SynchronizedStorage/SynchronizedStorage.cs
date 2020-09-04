namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Microsoft.Azure.Cosmos;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<CosmosClient>(SettingsKeys.CosmosClient);

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string");
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

            var partitionConfig = context.Settings.Get<PartitionAwareConfiguration>();

            if (partitionConfig is null)
            {
                throw new Exception("No message partition mappings were found. Use persistence.Partition() to configure mappings.");
            }

            context.Container.ConfigureComponent<StorageSessionFactory>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StorageSessionAdapter>(DependencyLifecycle.SingleInstance);
            context.Pipeline.Register(new PartitioningBehavior(databaseName, client, partitionConfig), "Partition Behavior");
        }
    }
}