namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Newtonsoft.Json;

    class EventSubscriptionDocument
    {
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }

        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty(PropertyName = "transportAddress")]
        public string TransportAddress { get; set; }

        [JsonProperty(PropertyName = "messageTypeName")]
        public string MessageTypeName { get; set; }
    }
}