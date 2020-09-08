namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Sagas;
    using Persistence;
    using Newtonsoft.Json;
    using System.IO;
    using Microsoft.Azure.Cosmos;

    class SagaPersister : ISagaPersister
    {
        JsonSerializer serializer;

        public SagaPersister(JsonSerializerSettings jsonSerializerSettings)
        {
            serializer = JsonSerializer.Create(jsonSerializerSettings);
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;

            storageSession.Modifications.Add(new SagaSave(sagaData, correlationProperty, context));
            return Task.CompletedTask;
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            storageSession.Modifications.Add(new SagaUpdate(sagaData, context));
            return Task.CompletedTask;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // reads need to go directly
            var container = storageSession.Container;
            var sagaIdAsString = sagaId.ToString();
            var partitionKey = storageSession.PartitionKey ?? new PartitionKey(sagaIdAsString);
            var responseMessage = await container.ReadItemStreamAsync(sagaIdAsString, partitionKey).ConfigureAwait(false);

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
            storageSession.Modifications.Add(new SagaDelete(sagaData, context));
            return Task.CompletedTask;
        }

        internal static readonly string SchemaVersion = "1.0.0";
    }
}