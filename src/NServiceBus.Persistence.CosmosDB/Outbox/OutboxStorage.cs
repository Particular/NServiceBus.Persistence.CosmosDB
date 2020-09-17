namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Newtonsoft.Json;

    class OutboxStorage : Feature
    {
        OutboxStorage()
        {
            Defaults(s =>
            {
                s.SetDefault(SettingsKeys.OutboxTimeToLiveInSeconds, (int)TimeSpan.FromDays(7).TotalSeconds);
                s.EnableFeatureByDefault<SynchronizedStorage>();
            });
            DependsOn<SynchronizedStorage>();
            DependsOn<Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var serializer = new JsonSerializer {ContractResolver = new CosmosDBContractResolver()};

            var ttlInSeconds = context.Settings.Get<int>(SettingsKeys.OutboxTimeToLiveInSeconds);

            context.Container.ConfigureComponent(builder => new OutboxPersister(builder.Build<ContainerHolderResolver>(), serializer, ttlInSeconds), DependencyLifecycle.SingleInstance);
            context.Pipeline.Register(builder => new LogicalOutboxBehavior(builder.Build<ContainerHolderResolver>(), serializer), "Behavior that mimics the outbox as part of the logical stage.");
        }
    }
}