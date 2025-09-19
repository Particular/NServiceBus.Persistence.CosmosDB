namespace NServiceBus.Persistence.CosmosDB;

using System.Linq;
using Features;
using Microsoft.Extensions.DependencyInjection;

sealed class Transaction : Feature
{
    public Transaction() => Defaults(s => s.SetDefault(new TransactionInformationConfiguration()));

    protected override void Setup(FeatureConfigurationContext context)
    {
        TransactionInformationConfiguration configuration = context.Settings.Get<TransactionInformationConfiguration>();

        // Register the ExtractorConfigurationHolder as a singleton with initial configuration
        // The configuration will be updated by the behaviors as they discover DI-registered extractors
        context.Services.AddSingleton(provider =>
        {
            // At this point we can only know about API-registered extractors
            // DI-registered extractors will be discovered and added by the behaviors
            return new ExtractorConfigurationHolder
            {
                Configuration = new ExtractorConfiguration
                {
                    HasCustomPartitionHeaderExtractors = configuration.PartitionKeyExtractor.HasCustomHeaderExtractors || provider.GetServices<IPartitionKeyFromHeadersExtractor>().Any(),
                    HasCustomPartitionMessageExtractors = configuration.PartitionKeyExtractor.HasCustomMessageExtractors || provider.GetServices<IPartitionKeyFromMessageExtractor>().Any(),
                    HasCustomContainerHeaderExtractors = configuration.ContainerInformationExtractor.HasCustomHeaderExtractors || provider.GetServices<IContainerInformationFromHeadersExtractor>().Any(),
                    HasCustomContainerMessageExtractors = configuration.ContainerInformationExtractor.HasCustomMessageExtractors || provider.GetServices<IContainerInformationFromMessagesExtractor>().Any()
                }
            };
        });

        // TODO: Dont add these if no custom extractors were added
        context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
        context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
    }
}