namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using Features;
    using Newtonsoft.Json;

    class OutboxStorage : Feature
    {
        OutboxStorage()
        {
            Defaults(s => s.EnableFeatureByDefault<SynchronizedStorage>());
            DependsOn<SynchronizedStorage>();
            DependsOn<Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var client = context.Settings.Get<ClientHolder>(SettingsKeys.CosmosClient).Client;
            var serializer = new JsonSerializer { ContractResolver = new CosmosDBContractResolver() };

            if (client is null)
            {
                throw new Exception("You must configure a CosmosClient or provide a connection string");
            }

            var partitionConfig = context.Settings.Get<PartitionAwareConfiguration>();

            if (partitionConfig is null)
            {
                throw new Exception("No message partition mappings were found. Use persistence.Partition() to configure mappings.");
            }


            context.Container.ConfigureComponent(builder => new OutboxPersister(builder.Build<ContainerHolder>(), serializer), DependencyLifecycle.SingleInstance);
            context.Pipeline.Register(new PartitioningBehavior(serializer), "Partition Behavior");
        }
    }
}