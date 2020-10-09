﻿namespace NServiceBus.Persistence.CosmosDB
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
        protected readonly OutboxRecord record;
        protected Stream stream = Stream.Null;

        protected OutboxOperation(OutboxRecord record, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context) : base(partitionKey, serializer, context)
        {
            this.record = record;
        }

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
            var jObject = JObject.FromObject(record, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.MajorVersion, GitVersionInformation.Major },
                { MetadataExtensions.MinorVersion, GitVersionInformation.Minor },
                { MetadataExtensions.OutboxDataContainerFullTypeNameMetadataKey, typeof(OutboxRecord).FullName }
            };
            jObject.Add(MetadataExtensions.MetadataKey, metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), partitionKeyPath);

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
            var jObject = JObject.FromObject(record, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.MajorVersion, GitVersionInformation.Major },
                { MetadataExtensions.MinorVersion, GitVersionInformation.Minor },
                { MetadataExtensions.OutboxDataContainerFullTypeNameMetadataKey, typeof(OutboxRecord).FullName }
            };
            jObject.Add(MetadataExtensions.MetadataKey, metadata);

            jObject.Add("ttl", ttlInSeconds);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), partitionKeyPath);

            // has to be kept open
            stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.UpsertItemStream(stream, options);
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The outbox record with id '{record.Id}' could not be marked as dispatched, it was updated by another process.");
        }
    }
}