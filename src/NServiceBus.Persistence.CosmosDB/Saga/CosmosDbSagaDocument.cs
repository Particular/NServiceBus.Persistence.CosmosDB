namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    class CosmosDbSagaDocument
    {
        public string PartitionKey { get; set; }

        public Guid SagaId { get; set; }

        public string EntityType { get; set; } = "Saga";

        public string SagaType { get; set; }

        public IContainSagaData SagaData { get; set; }

        public CosmosDbSagaMetadata MetaData { get; set; } = new CosmosDbSagaMetadata();
           
    }
}