namespace NServiceBus.Persistence.CosmosDB
{
    using System.Linq;
    using Features;
    using Microsoft.Extensions.DependencyInjection;

    class SynchronizedStorage : Feature
    {
        public SynchronizedStorage() =>
            // Depends on the core feature
            DependsOn<Features.SynchronizedStorage>();

        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Services.Any(descriptor => descriptor.ServiceType == typeof(IProvideCosmosClient)))
            {
                context.Services.AddSingleton(context.Settings.Get<IProvideCosmosClient>());
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

            ContainerInformation? defaultContainerInformation = null;
            if (context.Settings.TryGet<ContainerInformation>(out var info))
            {
                defaultContainerInformation = info;
            }

            context.Services.AddSingleton(b => new ContainerHolderResolver(b.GetService<IProvideCosmosClient>(), defaultContainerInformation, databaseName));

            context.Services.AddScoped<ICompletableSynchronizedStorageSession, CosmosSynchronizedStorageSession>();
            context.Services.AddTransient(sp => sp.GetRequiredService<ICompletableSynchronizedStorageSession>().CosmosPersistenceSession());
        }
    }
}