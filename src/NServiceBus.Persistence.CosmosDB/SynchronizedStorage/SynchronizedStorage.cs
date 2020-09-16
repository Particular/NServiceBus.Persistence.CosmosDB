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
            var containerName = context.Settings.Get<string>(SettingsKeys.ContainerName);
            var partitionKeyPath = context.Settings.Get<PartitionKeyPath>();

            var currentSharedTransactionalBatchHolder = new CurrentSharedTransactionalBatchHolder();

            context.Container.ConfigureComponent(_ => currentSharedTransactionalBatchHolder.Current.Create(), DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent(b => new ContainerHolder(b.Build<IProvideCosmosClient>().Client.GetContainer(databaseName, containerName), partitionKeyPath), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b=> new StorageSessionFactory(b.Build<ContainerHolder>(), currentSharedTransactionalBatchHolder), DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent(b=> new StorageSessionAdapter(currentSharedTransactionalBatchHolder), DependencyLifecycle.SingleInstance);

            context.Pipeline.Register(new CurrentSharedTransactionalBatchBehavior(currentSharedTransactionalBatchHolder), "Manages the lifecycle of the current storage session.");
        }
    }
}