namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Net;
    using System.Reflection;
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
        JsonSerializer serializer = new JsonSerializer();

        public SagaPersister(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var sagaType = context.GetSagaType();
            var sagaDataType = sagaData.GetType();
            var jObject = JObject.FromObject(sagaData);

            jObject.Add("PersisterVersion", FileVersionRetriever.GetFileVersion(typeof(SagaPersister)));
            jObject.Add("SagaType", sagaType.FullName);
            jObject.Add("SagaTypeVersion", FileVersionRetriever.GetFileVersion(sagaType));
            jObject.Add("SagaDataType", sagaDataType.FullName);
            jObject.Add("SagaDataTypeVersion", FileVersionRetriever.GetFileVersion(sagaDataType));

            using (var stream = new MemoryStream())
            using (var streamWriter = new StreamWriter(stream))
            using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                await jObject.WriteToAsync(jsonWriter).ConfigureAwait(false);

                await container.CreateItemStreamAsync(stream, new PartitionKey(partitionKey)).ConfigureAwait(false);
            }
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();

            // only update if we have the same version as in CosmosDB
            context.TryGet<string>("cosmosdb_etag", out var etag);
            var options = new ItemRequestOptions { IfMatchEtag = etag };

            using (var stream = new MemoryStream())
            using (var streamWriter = new StreamWriter(stream))
            using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
            {
                serializer.Serialize(jsonWriter, sagaData);

                return container.ReplaceItemStreamAsync(stream, partitionKey, new PartitionKey(partitionKey), options);
            }
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();
            try
            {
                var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

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
            catch (CosmosException exception) when(exception.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }
        }

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var propertyInfo = typeof(TSagaData).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                throw new Exception($"// TODO: what should be the exception here? Correlation property '{propertyName}' is not defined?");
            }

            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var sagaType = context.GetSagaType();
            var sagaId = SagaIdGenerator.Generate(sagaType, propertyValue);

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
    }

}