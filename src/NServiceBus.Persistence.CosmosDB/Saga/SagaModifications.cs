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

    abstract class SagaModification : Modification
    {
        public IContainSagaData SagaData { get; }

        protected SagaModification(IContainSagaData sagaData, ContextBag context) : base(context)
        {
            SagaData = sagaData;
        }

        public override PartitionKey PartitionKey => new PartitionKey(SagaData.Id.ToString());
        public override PartitionKeyPath PartitionKeyPath => new PartitionKeyPath("/Id");

        public override void Success(TransactionalBatchOperationResult result)
        {
            Context.Set($"cosmos_etag:{SagaData.Id}", result.ETag);
        }
    }

    sealed class SagaSave : SagaModification
    {
        public SagaCorrelationProperty CorrelationProperty { get; }

        public SagaSave(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ContextBag context) : base(sagaData, context)
        {
            CorrelationProperty = correlationProperty;
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' could not be created possibly due to a concurrency conflict.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(SagaData);

            jObject.Add("id", SagaData.Id.ToString());
            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

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

    sealed class SagaUpdate : SagaModification
    {
        public SagaUpdate(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' was updated by another process or no longer exists.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            var jObject = JObject.FromObject(SagaData);

            jObject.Add("id", SagaData.Id.ToString());
            var metadata = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SagaPersister.SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metadata);

            EnrichWithPartitionKeyIfNecessary(jObject, partitionKey, partitionKeyPath);

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

    sealed class SagaDelete : SagaModification
    {
        public SagaDelete(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' can't be completed because it was updated by another process.");
        }

        public override void Apply(TransactionalBatchDecorator transactionalBatch, PartitionKey partitionKey, PartitionKeyPath partitionKeyPath)
        {
            // only delete if we have the same version as in CosmosDB
            Context.TryGet<string>($"cosmos_etag:{SagaData.Id}", out var deleteEtag);
            var deleteOptions = new TransactionalBatchItemRequestOptions { IfMatchEtag = deleteEtag };
            transactionalBatch.DeleteItem(SagaData.Id.ToString(), deleteOptions);
        }
    }
}