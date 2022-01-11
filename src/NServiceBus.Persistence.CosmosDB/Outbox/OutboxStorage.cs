﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System;
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
                s.SetDefault(SettingsKeys.OutboxTimeToLiveInSeconds, (int)TimeSpan.FromDays(7).TotalSeconds);
                s.EnableFeatureByDefault<SynchronizedStorage>();
            });
            DependsOn<SynchronizedStorage>();
            DependsOn<Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            NonNativePubSubCheck.ThrowIfMessageDrivenPubSubInUse(context);

            var serializer = new JsonSerializer
            {
                ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver(),
                Converters = { new ReadOnlyMemoryConverter() }
            };

            var ttlInSeconds = context.Settings.Get<int>(SettingsKeys.OutboxTimeToLiveInSeconds);

            context.Services.AddSingleton<IOutboxStorage>(builder => new OutboxPersister(builder.GetService<ContainerHolderResolver>(), serializer, ttlInSeconds));
            context.Pipeline.Register("LogicalOutboxBehavior", builder => new OutboxBehavior(builder.GetService<ContainerHolderResolver>(), serializer), "Behavior that mimics the outbox as part of the logical stage.");
        }
    }
}