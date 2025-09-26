namespace NServiceBus.Persistence.CosmosDB;

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
        var serializer = new JsonSerializer
        {
            ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver(),
            Converters = { new ReadOnlyMemoryConverter() }
        };

        var configuration = context.Settings.Get<OutboxPersistenceConfiguration>();
        var transactionConfiguration = context.Settings.Get<TransactionInformationConfiguration>();

        context.Services.AddSingleton<IOutboxStorage>(builder => new OutboxPersister(
            builder.GetService<ContainerHolderResolver>(),
            serializer,
            configuration.PartitionKey,
            configuration.ReadFallbackEnabled,
            transactionConfiguration,
            (int)configuration.TimeToKeepDeduplicationData.TotalSeconds));

        context.Pipeline.Register("LogicalOutboxBehavior", builder => new LogicalOutboxBehavior(
            builder.GetService<ContainerHolderResolver>(),
            serializer,
            transactionConfiguration,
            configuration.ReadFallbackEnabled), "Behavior that mimics the outbox as part of the logical stage.");
    }

    internal const string ProcessorEndpointKey = "CosmosDB.TransactionalSession.ProcessorEndpoint";
}