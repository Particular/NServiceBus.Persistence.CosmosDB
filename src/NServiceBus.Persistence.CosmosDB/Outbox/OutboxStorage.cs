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
            var serializer = new JsonSerializer { ContractResolver = new CosmosDBContractResolver() };

            context.Container.ConfigureComponent(builder => new OutboxPersister(builder.Build<ContainerHolder>(), serializer), DependencyLifecycle.SingleInstance);
            context.Pipeline.Register(new PartitioningBehavior(serializer), "Partition Behavior");
        }
    }
}