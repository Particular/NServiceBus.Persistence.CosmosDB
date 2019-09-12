namespace NServiceBus.Persistence.CosmosDB
{
    class CosmosDbSagaMetadata
    {
        public string PersisterVersion { get; set; }

        public string SagaDataVersion { get; set; }
    }
}