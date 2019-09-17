﻿namespace NServiceBus.Persistence.CosmosDB
{
    static class WellKnownConfigurationKeys
    {
        public const string SagasConnectionString = "CosmosDB.Sagas.ConnectionString";
        public const string SagasDatabaseName = "CosmosDB.Sagas.DataBaseName";
        public const string SagasContainerName = "CosmosDB.Sagas.ContainerName";

        public const string SubscriptionsContainerName = "CosmosDB.Subscriptions.ContainerName";
    }
}