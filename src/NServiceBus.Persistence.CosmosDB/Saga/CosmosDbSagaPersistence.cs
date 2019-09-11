namespace NServiceBus.Features
{
    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent(b => new CosmosDbSagaPersister(), DependencyLifecycle.SingleInstance);
        }
    }
}