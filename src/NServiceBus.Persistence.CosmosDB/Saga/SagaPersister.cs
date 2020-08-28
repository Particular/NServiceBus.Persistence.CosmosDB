﻿namespace NServiceBus.Persistence.CosmosDB
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
        Container container;
        JsonSerializer serializer;

        public SagaPersister(JsonSerializerSettings jsonSerializerSettings, CosmosClient cosmosClient, string databaseName, string containerName)
        {
            container = cosmosClient.GetContainer(databaseName, containerName);
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

                var responseMessage = await container.CreateItemStreamAsync(stream, new PartitionKey(partitionKey)).ConfigureAwait(false);
                context.Set("cosmosdb_etag", responseMessage.Headers.ETag);
            }
        }

        public async Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var jObject = JObject.FromObject(sagaData, serializer);

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

                // ReSharper disable once UnusedVariable
                var responseMessage = await container.ReplaceItemStreamAsync(stream, partitionKey, new PartitionKey(partitionKey), options)
                    .ConfigureAwait(false);

                // check for conflict etc.
            }
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();
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

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            // TODO: currently we delete the item by ID. The idea is to use a document TTL to let CosmosDB remove the item.
            // TODO: this will allow developers to see that saga will be removed rather than not find it and wonder what happened.

            var partitionKey = sagaData.Id.ToString();

            // only delete if we have the same version as in CosmosDB
            context.TryGet<string>("cosmosdb_etag", out var etag);
            var options = new ItemRequestOptions { IfMatchEtag = etag };

            return container.DeleteItemStreamAsync(sagaData.Id.ToString(), new PartitionKey(partitionKey), options);
        }

        internal static readonly string SchemaVersion = "1.0.0";
    }
}