namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class OutboxStore : Operation
    {
        readonly JsonSerializer serializer;

        public OutboxRecord Record { get; }

        public OutboxStore(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(context, partitionKey, partitionKeyPath)
        {
            Record = record;
            this.serializer = serializer;
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(Record, serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.OutboxDataContainerSchemaVersionMetadataKey, OutboxPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey, metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), PartitionKeyPath);

            // has to be kept open
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.CreateItemStream(stream, options);
        }
    }
}