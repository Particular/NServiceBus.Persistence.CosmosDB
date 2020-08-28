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
    using Newtonsoft.Json.Linq;

    class SagaPersister : ISagaPersister
    {
        JsonSerializer serializer;

        public SagaPersister(JsonSerializerSettings jsonSerializerSettings, CosmosClient cosmosClient, string databaseName)
        {
            this.databaseName = databaseName;
            this.cosmosClient = cosmosClient;
            serializer = JsonSerializer.Create(jsonSerializerSettings);
        }

        public async Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var jObject = JObject.FromObject(sagaData, serializer);

            jObject.Add("id", partitionKey);
            var metaData = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metaData);

            using (var stream = new MemoryStream())
            using (var streamWriter = new StreamWriter(stream))
            using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                await jObject.WriteToAsync(jsonWriter).ConfigureAwait(false);
                await jsonWriter.FlushAsync().ConfigureAwait(false);

                // TODO use some kind of convention
                var container = cosmosClient.GetContainer(databaseName, sagaData.GetType().Name);
                var responseMessage = await container.CreateItemStreamAsync(stream, new PartitionKey(partitionKey)).ConfigureAwait(false);
                if(responseMessage.StatusCode == HttpStatusCode.Conflict || responseMessage.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw new Exception("TODO");
                }
                context.Set("cosmosdb_etag", responseMessage.Headers.ETag);
            }
        }

        public async Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var jObject = JObject.FromObject(sagaData, serializer);

            jObject.Add("id", partitionKey);
            var metaData = new JObject
            {
                { MetadataExtensions.SagaDataContainerSchemaVersionMetadataKey, SchemaVersion }
            };
            jObject.Add(MetadataExtensions.MetadataKey,metaData);

            // only update if we have the same version as in CosmosDB
            context.TryGet<string>("cosmosdb_etag", out var etag);
            var options = new ItemRequestOptions { IfMatchEtag = etag };

            using (var stream = new MemoryStream())
            using (var streamWriter = new StreamWriter(stream))
            using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                await jObject.WriteToAsync(jsonWriter).ConfigureAwait(false);
                await jsonWriter.FlushAsync().ConfigureAwait(false);

                // TODO use some kind of convention
                var container = cosmosClient.GetContainer(databaseName, sagaData.GetType().Name);
                // ReSharper disable once UnusedVariable
                var responseMessage = await container.ReplaceItemStreamAsync(stream, partitionKey, new PartitionKey(partitionKey), options)
                    .ConfigureAwait(false);

                if (responseMessage.StatusCode == HttpStatusCode.Conflict || responseMessage.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    throw new Exception($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' was updated by another process or no longer exists.");
                }
            }
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();

            // TODO use some kind of convention
            var container = cosmosClient.GetContainer(databaseName, typeof(TSagaData).Name);
            var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

            if(responseMessage.StatusCode == HttpStatusCode.NotFound || responseMessage.Content == null)
            {
                return default;
            }

            using (var streamReader = new StreamReader(responseMessage.Content))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var sagaData = serializer.Deserialize<TSagaData>(jsonReader);

                    context.Set("cosmosdb_etag", responseMessage.Headers.ETag);

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

        public async Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            // TODO: currently we delete the item by ID. The idea is to use a document TTL to let CosmosDB remove the item.
            // TODO: this will allow developers to see that saga will be removed rather than not find it and wonder what happened.

            var partitionKey = sagaData.Id.ToString();

            // only delete if we have the same version as in CosmosDB
            context.TryGet<string>("cosmosdb_etag", out var etag);
            var options = new ItemRequestOptions { IfMatchEtag = etag };

            // TODO use some kind of convention
            var container = cosmosClient.GetContainer(databaseName, sagaData.GetType().Name);
            var responseMessage = await container.DeleteItemStreamAsync(sagaData.Id.ToString(), new PartitionKey(partitionKey), options)
                .ConfigureAwait(false);

            if(responseMessage.StatusCode == HttpStatusCode.Conflict || responseMessage.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                throw new Exception($"The '{sagaData.GetType().Name}' saga with id '{sagaData.Id}' can't be completed because it was updated by another process.");
            }
        }

        internal static readonly string SchemaVersion = "1.0.0";
        CosmosClient cosmosClient;
        string databaseName;
    }
}