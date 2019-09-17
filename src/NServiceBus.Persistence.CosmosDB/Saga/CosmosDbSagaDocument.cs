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

        [JsonProperty(PropertyName = "sagaData")]
        public TSagaData SagaData { get; set; }

        [JsonProperty(PropertyName = "metaData")]
        public Dictionary<string, string> Metadata { get; set; }
    }
}