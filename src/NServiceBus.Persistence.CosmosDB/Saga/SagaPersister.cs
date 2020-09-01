namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Sagas;
    using Persistence;
    using Newtonsoft.Json;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json.Linq;

    class SagaPersister : ISagaPersister
    {
        JsonSerializer serializer;
        JsonSerializerSettings jsonSerializerSettings;

        public SagaPersister(JsonSerializerSettings jsonSerializerSettings)
        {
            this.jsonSerializerSettings = jsonSerializerSettings;
            serializer = JsonSerializer.Create(jsonSerializerSettings);
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;

            var partitionKey = JArray.Parse(storageSession.PartitionKey.ToString());
            var jObject = JObject.FromObject(sagaData, serializer);

            jObject.Add("id", sagaData.Id.ToString());
            jObject.Add("partitionKey", partitionKey[0]);
            var metaData = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metaData);

            // has to be kept open, implement tracking
            // couldn't get the stream writer to work properly
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject, jsonSerializerSettings)));

            var options = new TransactionalBatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };
            storageSession.TransactionalBatch.CreateItemStream(stream, options);
            // need to figure out how to get back the ETag for creations
            return Task.CompletedTask;
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;

            var partitionKey = storageSession.PartitionKey.ToString();
            var jObject = JObject.FromObject(sagaData, serializer);

            jObject.Add("id", sagaData.Id.ToString());
            jObject.Add("partitionKey", partitionKey);
            var metaData = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metaData);

            // only update if we have the same version as in CosmosDB
            context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var etag);
            var options = new TransactionalBatchItemRequestOptions
            {
                IfMatchEtag = etag,
                EnableContentResponseOnWrite = false,
            };

            // has to be kept open, implement tracking
            // couldn't get the stream writer to work properly
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject, jsonSerializerSettings)));
            storageSession.TransactionalBatch.ReplaceItemStream(sagaData.Id.ToString(), stream, options);
            return Task.CompletedTask;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // reads need to go directly
            var container = storageSession.Container;
            var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), storageSession.PartitionKey).ConfigureAwait(false);

            if(responseMessage.StatusCode == HttpStatusCode.NotFound || responseMessage.Content == null)
            {
                return default;
            }

            using (var streamReader = new StreamReader(responseMessage.Content))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var sagaData = serializer.Deserialize<TSagaData>(jsonReader);

                    context.Set($"cosmos_etag:{sagaId}", responseMessage.Headers.ETag);

                    return sagaData;
                }
            }
        }

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var sagaId = SagaIdGenerator.Generate(typeof(TSagaData), propertyName, propertyValue);

            return Get<TSagaData>(sagaId, session, context);
        }

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            // TODO: currently we delete the item by ID. The idea is to use a document TTL to let CosmosDB remove the item.
            // TODO: this will allow developers to see that saga will be removed rather than not find it and wonder what happened.

            var storageSession = (StorageSession)session;

            // only delete if we have the same version as in CosmosDB
            context.TryGet<string>($"cosmos_etag:{sagaData.Id}", out var etag);
            var options = new TransactionalBatchItemRequestOptions { IfMatchEtag = etag };

            storageSession.TransactionalBatch.DeleteItem(sagaData.Id.ToString(), options);
            return Task.CompletedTask;
        }

        internal static readonly string SchemaVersion = "1.0.0";
    }
}