namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Text;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Sagas;

    abstract class SagaOperation : Operation
    {
        public IContainSagaData SagaData { get; }

        protected SagaOperation(IContainSagaData sagaData, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(partitionKey, partitionKeyPath, serializer, context)
        {
            SagaData = sagaData;
        }

        public override void Success(TransactionalBatchOperationResult result)
        {
            Context.Set($"cosmos_etag:{SagaData.Id}", result.ETag);
        }
    }

    sealed class SagaSave : SagaOperation
    {
        public SagaCorrelationProperty CorrelationProperty { get; }

        public SagaSave(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context)
            : base(sagaData, partitionKey, partitionKeyPath, serializer, context)
        {
            CorrelationProperty = correlationProperty;
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' could not be created possibly due to a concurrency conflict.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(SagaData, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), PartitionKeyPath);

            // Has to be kept open for transaction batch to be able to use the stream
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            transactionalBatch.CreateItemStream(stream, options);
        }
    }

    sealed class SagaUpdate : SagaOperation
    {
        public SagaUpdate(IContainSagaData sagaData, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, JsonSerializer serializer, ContextBag context) : base(sagaData, partitionKey, partitionKeyPath, serializer, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' was updated by another process or no longer exists.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            var jObject = JObject.FromObject(SagaData, Serializer);

            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

            jObject.EnrichWithPartitionKeyIfNecessary(PartitionKey.ToString(), PartitionKeyPath);

            // only update if we have the same version as in CosmosDB
            Context.TryGet<string>($"cosmos_etag:{SagaData.Id}", out var updateEtag);
            var options = new TransactionalBatchItemRequestOptions
            {
                IfMatchEtag = updateEtag,
                EnableContentResponseOnWrite = false,
            };

            // has to be kept open
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject)));
            transactionalBatch.ReplaceItemStream(SagaData.Id.ToString(), stream, options);
        }
    }

    sealed class SagaDelete : SagaOperation
    {
        public SagaDelete(IContainSagaData sagaData, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath, ContextBag context) : base(sagaData, partitionKey, partitionKeyPath, null, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' can't be completed because it was updated by another process.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch)
        {
            // only delete if we have the same version as in CosmosDB
            Context.TryGet<string>($"cosmos_etag:{SagaData.Id}", out var deleteEtag);
            var deleteOptions = new TransactionalBatchItemRequestOptions { IfMatchEtag = deleteEtag };
            transactionalBatch.DeleteItem(SagaData.Id.ToString(), deleteOptions);
        }
    }
}
