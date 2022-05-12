namespace NServiceBus.Persistence.CosmosDB
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Sagas;

    class SagaStorage : Feature
    {
        internal SagaStorage()
        {
            Defaults(s =>
            {
                s.SetDefault<ISagaIdGenerator>(new SagaIdGenerator());
                s.EnableFeatureByDefault<SynchronizedStorage>();
            });

            DependsOn<Sagas>();
            DependsOn<SynchronizedStorage>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            NonNativePubSubCheck.ThrowIfMessageDrivenPubSubInUse(context);

            var sagaConfiguration = context.Settings.GetOrDefault<SagaPersistenceConfiguration>() ?? new SagaPersistenceConfiguration();
            var pessimisticLockingConfiguration = sagaConfiguration.PessimisticLockingConfiguration;
            if (pessimisticLockingConfiguration.PessimisticLockingEnabled)
            {
                pessimisticLockingConfiguration.ValidateRefreshDelays();
            }

            var serializer = new JsonSerializer { ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver() };

            context.Services.AddSingleton<ISagaPersister>(builder => new SagaPersister(serializer, sagaConfiguration));
        }
    }
}