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

        context.Services.AddSingleton(provider =>
        {
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

        context.Pipeline.Register(new TransactionInformationBeforeTheLogicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
        context.Pipeline.Register(new TransactionInformationBeforeThePhysicalOutboxBehavior.RegisterStep(configuration.PartitionKeyExtractor, configuration.ContainerInformationExtractor));
    }
}