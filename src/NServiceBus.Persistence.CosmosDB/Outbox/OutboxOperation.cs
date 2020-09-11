﻿namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class OutboxOperation : Operation
    {
        public OutboxRecord Record { get;  }

        protected OutboxOperation(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(partitionKey, partitionKeyPath, serializer, context)
        {
            Record = record;
        }
        public override void Success(TransactionalBatchOperationResult result)
        {
            Context.Set($"cosmos_etag:{Record.Id}", result.ETag);
        }
    }

    class OutboxStore : OutboxOperation
    {
        public OutboxStore(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(record, partitionKey, partitionKeyPath, serializer, context)
        {
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(Record, Serializer);

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


    class OutboxDelete : OutboxOperation
    {
        public OutboxDelete(OutboxRecord record, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(record, partitionKey, partitionKeyPath, serializer, context)
        {
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(Record, Serializer);
            // TODO: Make TTL configurable
            jObject.Add("ttl", 100);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), PartitionKeyPath);

            // has to be kept open
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(jObject.ToString(Formatting.None)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.UpsertItemStream(stream, options);
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The outbox record with id '{Record.Id}' could not be marked as dispatched, it was updated by another process.");
        }
    }
}