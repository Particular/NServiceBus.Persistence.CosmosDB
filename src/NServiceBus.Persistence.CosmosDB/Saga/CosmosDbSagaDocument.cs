namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    class CosmosDbSagaDocument<TSagaData>
    {
        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid SagaId { get; set; }

        public string EntityType { get; set; }

        public string SagaType { get; set; }

        public TSagaData SagaData { get; set; }

        public Dictionary<string, string> Metadata { get; set; }
           
    }
}