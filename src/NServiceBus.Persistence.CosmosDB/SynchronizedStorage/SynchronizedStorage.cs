namespace NServiceBus.Persistence.CosmosDB
{
    using System.Linq;
    using Features;
    using Microsoft.Extensions.DependencyInjection;

    class SynchronizedStorage : Feature
    {
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

            var currentSharedTransactionalBatchHolder = new CurrentSharedTransactionalBatchHolder();

            context.Services.AddTransient(_ => currentSharedTransactionalBatchHolder.Current.Create());

            context.Services.AddTransient(b => new ContainerHolderResolver(b.GetService<IProvideCosmosClient>(), defaultContainerInformation, databaseName));
            context.Services.AddTransient(b => new StorageSessionFactory(b.GetService<ContainerHolderResolver>(), currentSharedTransactionalBatchHolder));
            context.Services.AddTransient(b => new StorageSessionAdapter(currentSharedTransactionalBatchHolder));

            context.Pipeline.Register(new CurrentSharedTransactionalBatchBehavior(currentSharedTransactionalBatchHolder), "Manages the lifecycle of the current storage session.");
        }
    }
}