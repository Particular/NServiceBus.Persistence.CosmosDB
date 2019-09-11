namespace NServiceBus.Features
{
    using NServiceBus.Sagas;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.RegisterSingleton<ISagaPersister>(new SagaPersister());
        }
    }
}