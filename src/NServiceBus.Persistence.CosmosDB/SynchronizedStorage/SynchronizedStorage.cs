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

            context.Container.ConfigureComponent(b => new ContainerHolderResolver(b.Build<IProvideCosmosClient>(), defaultContainerInformation, databaseName), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b => new StorageSessionFactory(b.Build<ContainerHolderResolver>()), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b => new StorageSessionAdapter(), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b => b.Build<CompletableSynchronizedStorageSessionAdapter>().AdaptedSession.CosmosPersistenceSession(), DependencyLifecycle.InstancePerUnitOfWork);
        }
    }
}