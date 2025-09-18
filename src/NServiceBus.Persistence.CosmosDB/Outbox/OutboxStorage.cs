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

        //int ttlInSeconds = context.Settings.Get<int>(SettingsKeys.OutboxTimeToLiveInSeconds);
        //var endpointName = context.Settings.GetOrDefault<string>(ProcessorEndpointKey) ?? context.Settings.EndpointName();
        var configuration = context.Settings.Get<OutboxPersistenceConfiguration>();

        // Check if custom PartitionKeyExtractors are used. If so, we need to adjust the Partition Key logic in OutboxPersister
        var transactionConfig = context.Settings.Get<TransactionInformationConfiguration>();
        var extractorConfig = new ExtractorConfiguration
        {
            HasCustomPartitionHeaderExtractors = transactionConfig.PartitionKeyExtractor.HasCustomHeaderExtractors,
            HasCustomPartitionMessageExtractors = transactionConfig.PartitionKeyExtractor.HasCustomMessageExtractors,
            HasCustomContainerHeaderExtractors = transactionConfig.ContainerInformationExtractor.HasCustomHeaderExtractors,
            HasCustomContainerMessageExtractors = transactionConfig.ContainerInformationExtractor.HasCustomMessageExtractors
        };

        context.Services.AddSingleton<IOutboxStorage>(builder => new OutboxPersister(builder.GetService<ContainerHolderResolver>(), serializer, configuration.PartitionKey, configuration.ReadFallbackEnabled, extractorConfig, (int)configuration.TimeToKeepDeduplicationData.TotalSeconds));
        context.Pipeline.Register("LogicalOutboxBehavior", builder => new LogicalOutboxBehavior(builder.GetService<ContainerHolderResolver>(), serializer), "Behavior that mimics the outbox as part of the logical stage.");
    }

    internal const string ProcessorEndpointKey = "CosmosDB.TransactionalSession.ProcessorEndpoint";
}