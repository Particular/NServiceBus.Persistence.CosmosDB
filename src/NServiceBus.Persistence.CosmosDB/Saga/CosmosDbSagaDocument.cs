namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Newtonsoft.Json;

    class CosmosDbSagaDocument
    {
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid SagaId { get; set; }

        public string EntityType { get; set; }

        public string SagaType { get; set; }

        public object SagaData { get; set; }

        public CosmosDbSagaMetadata Metadata { get; set; } = new CosmosDbSagaMetadata();
           
    }
}