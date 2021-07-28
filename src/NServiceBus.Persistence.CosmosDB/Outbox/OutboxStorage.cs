namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
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
            var serializer = new JsonSerializer { ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver() };

            var ttlInSeconds = context.Settings.Get<int>(SettingsKeys.OutboxTimeToLiveInSeconds);

            context.Services.AddTransient(builder => new OutboxPersister(builder.GetService<ContainerHolderResolver>(), serializer, ttlInSeconds));
            context.Pipeline.Register(builder => new LogicalOutboxBehavior(builder.GetService<ContainerHolderResolver>(), serializer), "Behavior that mimics the outbox as part of the logical stage.");
        }
    }
}