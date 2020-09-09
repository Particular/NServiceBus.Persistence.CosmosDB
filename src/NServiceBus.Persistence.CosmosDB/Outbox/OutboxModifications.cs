namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class OutboxModification : Modification
    {
        public OutboxRecord Record { get;  }

        protected OutboxModification(OutboxRecord record, ContextBag context) : base(context)
        {
            Record = record;
        }

        // should never be called
        public override PartitionKey PartitionKey => throw new InvalidOperationException();
        public override PartitionKeyPath PartitionKeyPath => throw new InvalidOperationException();
    }

    class OutboxStore : OutboxModification
    {
        public OutboxStore(OutboxRecord record, ContextBag context) : base(record, context)
        {
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(Record);

            jObject.Add("id", Record.Id);
            var metadata = new JObject
            {
                { MetadataExtensions.OutboxDataContainerSchemaVersionMetadataKey, OutboxPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey, metadata);

            EnrichWithPartitionKeyIfNecessary(jObject, partitionKey, partitionKeyPath);

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