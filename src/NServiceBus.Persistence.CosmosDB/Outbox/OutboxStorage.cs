namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Features;

    class OutboxStorage : Feature
    {
        OutboxStorage()
        {
            Defaults(s => s.EnableFeatureByDefault<SynchronizedStorage>());
            DependsOn<Features.Outbox>();
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

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

            context.Container.ConfigureComponent(() => new OutboxPersister(), DependencyLifecycle.SingleInstance);

            context.Pipeline.Register(new PartitioningBehavior(databaseName, client, partitionConfig), "Partition Behavior");
        }
    }
}
