namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
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
                s.SetDefault(SettingsKeys.LeaseLockTime, TimeSpan.FromSeconds(60));
                s.SetDefault(SettingsKeys.LeaseLockAcquisitionMaximumRefreshDelay, TimeSpan.FromMilliseconds(20));
            });
            DependsOn<SynchronizedStorage>();
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            NonNativePubSubCheck.ThrowIfMessageDrivenPubSubInUse(context);

            var serializer = new JsonSerializer { ContractResolver = new UpperCaseIdIntoLowerCaseIdContractResolver() };

            var migrationModeEnabled = context.Settings.GetOrDefault<bool>(SettingsKeys.EnableMigrationMode);
            var usePessimisticsLockingMode = context.Settings.GetOrDefault<bool>(SettingsKeys.EnablePessimisticsLocking);
            var leaseLockTime = context.Settings.Get<TimeSpan>(SettingsKeys.LeaseLockTime);
            var LeaseLockAcquisitionMaximumRefreshDelay = context.Settings.Get<TimeSpan>(SettingsKeys.LeaseLockAcquisitionMaximumRefreshDelay);

            context.Services.AddSingleton<ISagaPersister>(builder => new SagaPersister(serializer, migrationModeEnabled, usePessimisticsLockingMode, leaseLockTime, LeaseLockAcquisitionMaximumRefreshDelay));
        }
    }
}