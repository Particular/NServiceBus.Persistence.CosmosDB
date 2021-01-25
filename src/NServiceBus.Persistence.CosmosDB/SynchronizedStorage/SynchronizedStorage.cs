namespace NServiceBus.Persistence.CosmosDB
{
    using Features;

    class SynchronizedStorage : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Container.HasComponent<IProvideCosmosClient>())
            {
                context.Container.ConfigureComponent(context.Settings.Get<IProvideCosmosClient>, DependencyLifecycle.SingleInstance);
            }

            var databaseName = context.Settings.Get<string>(SettingsKeys.DatabaseName);

            ContainerInformation? defaultContainerInformation = null;
            if (context.Settings.TryGet<ContainerInformation>(out var info))
            {
                defaultContainerInformation = info;
            }

            var currentSharedTransactionalBatchHolder = new CurrentSharedTransactionalBatchHolder();

            context.Container.ConfigureComponent(_ => currentSharedTransactionalBatchHolder.Current.Create(), DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent(b => new ContainerHolderResolver(b.Build<IProvideCosmosClient>(), defaultContainerInformation, databaseName), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b => new StorageSessionFactory(b.Build<ContainerHolderResolver>(), currentSharedTransactionalBatchHolder), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b => new StorageSessionAdapter(currentSharedTransactionalBatchHolder), DependencyLifecycle.SingleInstance);

            context.Pipeline.Register(new CurrentSharedTransactionalBatchBehavior(currentSharedTransactionalBatchHolder), "Manages the lifecycle of the current storage session.");
        }
    }
}