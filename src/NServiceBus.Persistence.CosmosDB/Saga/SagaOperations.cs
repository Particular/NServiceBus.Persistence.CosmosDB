namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    abstract class SagaOperation : Operation
    {
        protected readonly IContainSagaData sagaData;
        protected Stream stream = Stream.Null;

        protected SagaOperation(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context) : base(partitionKey, serializer, context)
        {
            this.sagaData = sagaData;
        }

        public override void Success(TransactionalBatchOperationResult result)
        {
            Context.Set($"cosmos_etag:{sagaData.Id}", result.ETag);
        }

        public override void Dispose()
        {
            stream.Dispose();
        }
    }

    sealed class SagaSave : SagaOperation
    {
        public SagaSave(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
            : base(sagaData, partitionKey, serializer, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' could not be created possibly due to a concurrency conflict.");
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(sagaData, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion },
                { MetadataExtensions.SagaDataContainerFullTypeNameMetadataKey, sagaData.GetType().FullName }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), partitionKeyPath);

            // Has to be kept open for transaction batch to be able to use the stream
            stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.CreateItemStream(stream, options);
        }
    }

    sealed class SagaUpdate : SagaOperation
    {
        public SagaUpdate(IContainSagaData sagaData, PartitionKey partitionKey, JsonSerializer serializer, ContextBag context) : base(sagaData, partitionKey, serializer, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' was updated by another process or no longer exists.");
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(sagaData, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion },
                { MetadataExtensions.SagaDataContainerFullTypeNameMetadataKey, sagaData.GetType().FullName }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), partitionKeyPath);

            // only update if we have the same version as in CosmosDB
            Context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var updateEtag);
            var options = new TransactionalBatchItemRequestOptions
            {
                IfMatchEtag = updateEtag,
                EnableContentResponseOnWrite = false,
            };

            // has to be kept open
            stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            transactionalBatch.ReplaceItemStream(sagaData.Id.ToString(), stream, options);
        }
    }

    sealed class SagaDelete : SagaOperation
    {
        public SagaDelete(IContainSagaData sagaData, PartitionKey partitionKey, ContextBag context) : base(sagaData, partitionKey, null, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' can't be completed because it was updated by another process.");
        }

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
            // only delete if we have the same version as in CosmosDB
            Context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var deleteEtag);
            var deleteOptions = new TransactionalBatchItemRequestOptions { IfMatchEtag = deleteEtag };
            transactionalBatch.DeleteItem(sagaData.Id.ToString(), deleteOptions);
        }
    }
}
