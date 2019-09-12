namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    class CosmosDbSagaDocument<T>
    {
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid SagaId { get; set; }

        public string EntityType { get; set; }

        public string SagaType { get; set; }

        public T SagaData { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
           
    }
}