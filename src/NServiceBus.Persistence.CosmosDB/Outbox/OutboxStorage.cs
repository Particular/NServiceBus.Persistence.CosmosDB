namespace NServiceBus.Persistence.CosmosDB
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Outbox;

    class OutboxStorage : Feature
    {
        OutboxStorage()
        {
            Defaults(s =>
            {
                s.SetDefault(new OutboxPersistenceConfiguration { PartitionKey = s.EndpointName() });
                s.EnableFeatureByDefault<SynchronizedStorage>();
            });

            DependsOn<Outbox>();
            DependsOn<SynchronizedStorage>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            NonNativePubSubCheck.ThrowIfMessageDrivenPubSubInUse(context);

            var serializer = new JsonSerializer
            {
                ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver(),
                Converters = { new ReadOnlyMemoryConverter() }
            };

            var configuration = context.Settings.Get<OutboxPersistenceConfiguration>();

            context.Services.AddSingleton<IOutboxStorage>(builder => new OutboxPersister(
                builder.GetService<ContainerHolderResolver>(),
                serializer,
                configuration.PartitionKey,
                configuration.ReadFallbackEnabled,
                (int)configuration.TimeToKeepDeduplicationData.TotalSeconds));

            context.Pipeline.Register("LogicalOutboxBehavior", builder => new OutboxBehavior(
                builder.GetService<ContainerHolderResolver>(),
                serializer,
                configuration.ReadFallbackEnabled), "Behavior that mimics the outbox as part of the logical stage.");
        }
    }
}