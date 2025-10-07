namespace NServiceBus;

using NServiceBus.Features;
using NServiceBus.Logging;

class ContainerWarningFeature : Feature
{
    public ContainerWarningFeature()
    {
        EnableByDefault();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var hasDefaultContainer = context.Settings.TryGet(out ContainerInformation _);
        var persistenceEnabled = context.Settings.TryGet<PersistenceExtensions<CosmosPersistence>>(out var persistence);
        var hasSetNewBehaviour = context.Settings.GetOrDefault<bool>(CosmosPersistenceConfig.EnableContainerFromMessageExtractorKey);

        if (persistenceEnabled && persistence.TransactionInformation().HasCustomContainerMessageExtractors && hasDefaultContainer && !hasSetNewBehaviour)
        {
            log.Warn("The current endpoint setup has both default container and message container extractors configured, but does not have `EnableContainerFromMessageExtractor` set.");
        }
    }

    static readonly ILog log = LogManager.GetLogger<ContainerWarningFeature>();
}
