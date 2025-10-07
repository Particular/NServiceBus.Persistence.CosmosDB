namespace NServiceBus;

using NServiceBus.Features;
using NServiceBus.Logging;
using NServiceBus.Persistence.CosmosDB;

class ContainerWarningFeature : Feature
{
    public ContainerWarningFeature()
    {
        EnableByDefault();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var hasDefaultContainer = context.Settings.TryGet(out ContainerInformation _);
        var hasTransactionInformation = context.Settings.TryGet(out TransactionInformationConfiguration transactionInformation);
        var hasSetNewBehaviour = context.Settings.GetOrDefault<bool>(CosmosPersistenceConfig.EnableContainerFromMessageExtractorKey);

        if (hasTransactionInformation && transactionInformation.HasCustomContainerMessageExtractors && hasDefaultContainer && !hasSetNewBehaviour)
        {
            log.Warn("The current endpoint setup has both default container and message container extractors configured, but does not have `EnableContainerFromMessageExtractor` set.");
        }
    }

    static readonly ILog log = LogManager.GetLogger<ContainerWarningFeature>();
}
