namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class OutboxOperation : Operation
    {
        public OutboxRecord Record { get;  }

        protected OutboxOperation(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, ContextBag context) : base(context, partitionKey, partitionKeyPath)
        {
            Record = record;
        }
    }

    class OutboxStore : OutboxOperation
    {
        public OutboxStore(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, ContextBag context) : base(record, partitionKey, partitionKeyPath, context)
        {
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(Record);

            jObject.Add("id", Record.Id);
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