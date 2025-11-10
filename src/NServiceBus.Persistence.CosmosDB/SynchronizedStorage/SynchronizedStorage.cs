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

        var installerSettings = context.Settings.Get<InstallerSettings>();
        if (!installerSettings.Disabled)
        {
            context.AddInstaller<Installer>();
        }

        string databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

        ContainerInformation? defaultContainerInformation = null;
        if (context.Settings.TryGet<ContainerInformation>(out ContainerInformation info))
        {
            defaultContainerInformation = info;
        }

        context.Services.AddSingleton(b => new ContainerHolderResolver(b.GetService<IProvideCosmosClient>(), defaultContainerInformation, databaseName));

        context.Services.AddScoped<ICompletableSynchronizedStorageSession, CosmosSynchronizedStorageSession>();
        context.Services.AddScoped(sp => (sp.GetService<ICompletableSynchronizedStorageSession>() as IWorkWithSharedTransactionalBatch)?.Create());
    }
}