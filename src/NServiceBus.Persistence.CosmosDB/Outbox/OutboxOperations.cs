namespace NServiceBus.Persistence.CosmosDB
{
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class OutboxOperation : Operation
    {
        protected readonly OutboxRecord record;
        protected Stream stream = Stream.Null;

        static JObject metadata;

        static JObject Metadata => metadata ??= new JObject
        {
            { MetadataExtensions.OutboxDataContainerSchemaVersionMetadataKey, OutboxPersister.SchemaVersion },
            { MetadataExtensions.OutboxDataContainerFullTypeNameMetadataKey, typeof(OutboxRecord).FullName }
        };

        protected OutboxOperation(OutboxRecord record, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context) : base(partitionKey, serializer, context)
        {
            this.record = record;
        }

        protected JObject ToEnrichedJObject(PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(record, Serializer);

            EnrichWithOutboxMetadata(jObject);

            EnrichWithPartitionKeyIfNecessary(jObject, partitionKeyPath);

            return jObject;
        }

        void EnrichWithOutboxMetadata(JObject toBeEnriched) => toBeEnriched.Add(MetadataExtensions.MetadataKey, Metadata);

        public override void Dispose()
        {
            stream.Dispose();
        }
    }

    sealed class OutboxStore : OutboxOperation
    {
        public OutboxStore(OutboxRecord record, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context) : base(record, partitionKey, serializer, context)
        {
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            var jObject = ToEnrichedJObject(partitionKeyPath);

            // has to be kept open
            stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.CreateItemStream(stream, options);
        }
    }


    sealed class OutboxDelete : OutboxOperation
    {
        readonly int ttlInSeconds;

        public OutboxDelete(OutboxRecord record, PartitionKey partitionKey, JsonSerializer serializer, int ttlInSeconds, ContextBag context) : base(record, partitionKey, serializer, context)
        {
            this.ttlInSeconds = ttlInSeconds;
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            var jObject = ToEnrichedJObject(partitionKeyPath);

            jObject.Add("ttl", ttlInSeconds);

            // has to be kept open
            stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            // deliberately not setting the ETag here because setting as dispatched is idempotent.
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.UpsertItemStream(stream, options);
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new TransactionalBatchOperationException($"The outbox record with id '{record.Id}' could not be marked as dispatched. Response status code: {result.StatusCode}.", result);
        }
    }
}