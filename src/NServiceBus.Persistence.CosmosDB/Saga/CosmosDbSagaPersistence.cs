namespace NServiceBus.Persistence.CosmosDB
{
    using Features;
    using Newtonsoft.Json;
    using Sagas;

    class CosmosDbSagaPersistence : Feature
    {
        internal CosmosDbSagaPersistence()
        {
            Defaults(s =>
            {
                s.EnableFeatureByDefault<SynchronizedStorage>();
                s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator());
            });
            DependsOn<SynchronizedStorage>();
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var serializer = new JsonSerializer {ContractResolver = new CosmosDBContractResolver()};

            context.Container.ConfigureComponent(builder => new SagaPersister(serializer), DependencyLifecycle.SingleInstance);
        }
    }
}