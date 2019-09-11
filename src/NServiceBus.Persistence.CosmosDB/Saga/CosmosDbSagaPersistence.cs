namespace NServiceBus.Features
{
    using NServiceBus.Sagas;
    using Persistence.CosmosDB;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            DependsOn<Sagas>();
            Defaults(s => s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator()));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.RegisterSingleton<ISagaPersister>(new SagaPersister());
        }
    }
}