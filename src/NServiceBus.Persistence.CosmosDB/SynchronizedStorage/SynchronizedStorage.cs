namespace NServiceBus.Persistence.CosmosDB;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

class SynchronizedStorage : Feature
{
    public SynchronizedStorage() =>
        // Depends on the core feature
        DependsOn<Features.SynchronizedStorage>();

    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Services.TryAddSingleton(context.Settings.Get<IProvideCosmosClient>());

        string databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

        context.Services.AddSingleton(b =>
        {
            ContainerInformation? defaultContainerInformation = null;

            // Only set the defaultContainerInformation if there are no custom extractors
            var extractorConfigHolder = b.GetService<ExtractorConfigurationHolder>();
            bool hasCustomExtractors = extractorConfigHolder != null &&
                                      extractorConfigHolder.Configuration.HasAnyCustomContainerExtractors;

            if (!hasCustomExtractors && context.Settings.TryGet<ContainerInformation>(out ContainerInformation info))
            {
                defaultContainerInformation = info;
            }

            return new ContainerHolderResolver(b.GetService<IProvideCosmosClient>(), defaultContainerInformation, databaseName);
        });

        context.Services.AddScoped<ICompletableSynchronizedStorageSession, CosmosSynchronizedStorageSession>();
        context.Services.AddScoped(sp => (sp.GetService<ICompletableSynchronizedStorageSession>() as IWorkWithSharedTransactionalBatch)?.Create());
    }
}