namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using Features;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    class OutboxStorage : Feature
    {
        OutboxStorage()
        {
            Defaults(s => s.EnableFeatureByDefault<SynchronizedStorage>());
            DependsOn<Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<CosmosClient>(SettingsKeys.CosmosClient);

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string");
            }

            var partitionConfig = context.Settings.Get<PartitionAwareConfiguration>();

            if (partitionConfig is null)
            {
                throw new Exception("No message partition mappings were found. Use persistence.Partition() to configure mappings.");
            }

            var serializerSettings = context.Settings.Get<JsonSerializerSettings>(SettingsKeys.Sagas.JsonSerializerSettings);

            context.Container.ConfigureComponent(() => new OutboxPersister(serializerSettings), DependencyLifecycle.SingleInstance);
        }
    }
}