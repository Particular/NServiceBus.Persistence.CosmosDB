namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Sagas;

    class SagaPersister : ISagaPersister
    {
        public SagaPersister(JsonSerializer serializer, bool migrationModeEnabled)
        {
            this.serializer = serializer;
            this.migrationModeEnabled = migrationModeEnabled;
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaSave(sagaData, partitionKey, serializer, context));
            return Task.CompletedTask;
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaUpdate(sagaData, partitionKey, serializer, context));
            return Task.CompletedTask;
        }

        async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context, bool isGeneratedSagaId) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // reads need to go directly
            var container = storageSession.ContainerHolder.Container;
            var partitionKey = GetPartitionKey(context, sagaId);

            using (var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), partitionKey).ConfigureAwait(false))
            {
                var sagaStream = responseMessage.Content;

                var sagaNotFound = responseMessage.StatusCode == HttpStatusCode.NotFound || sagaStream == null;

                if (sagaNotFound && !migrationModeEnabled)
                {
                    return default;
                }

                if (sagaNotFound && migrationModeEnabled && !isGeneratedSagaId)
                {
                    var query = $@"SELECT TOP 1 * FROM c WHERE c[""{MetadataExtensions.MetadataKey}""][""{MetadataExtensions.SagaDataContainerMigratedSagaIdMetadataKey}""] = '{sagaId}'";
                    var queryDefinition = new QueryDefinition(query);
                    var queryStreamIterator = container.GetItemQueryStreamIterator(queryDefinition);

                    using (var iteratorResponse = await queryStreamIterator.ReadNextAsync().ConfigureAwait(false))
                    {
                        iteratorResponse.EnsureSuccessStatusCode();

                        using (var streamReader = new StreamReader(iteratorResponse.Content))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var iteratorResult = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);

                            if (!(iteratorResult["Documents"] is JArray documents) || !documents.HasValues)
                            {
                                return default;
                            }

                            var sagaData = documents[0].ToObject<TSagaData>(serializer);
                            context.Set($"cosmos_etag:{sagaData.Id}", responseMessage.Headers.ETag);
                            context.Set($"cosmos_migratedsagaid:{sagaData.Id}", sagaId);
                            return sagaData;
                        }
                    }
                }

                using(sagaStream)
                using (var streamReader = new StreamReader(sagaStream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var sagaData = serializer.Deserialize<TSagaData>(jsonReader);

                    context.Set($"cosmos_etag:{sagaData.Id}", responseMessage.Headers.ETag);

                    return sagaData;
                }
            }
        }

        public Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData =>
            Get<TSagaData>(sagaId, session, context, false);

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var sagaId = SagaIdGenerator.Generate(typeof(TSagaData), propertyName, propertyValue);

            return Get<TSagaData>(sagaId, session, context, true);
        }

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaDelete(sagaData, partitionKey, context));

            return Task.CompletedTask;
        }

        static PartitionKey GetPartitionKey(ContextBag context, Guid sagaDataId)
        {
            if (!context.TryGet<PartitionKey>(out var partitionKey))
            {
                partitionKey = new PartitionKey(sagaDataId.ToString());
            }

            return partitionKey;
        }

        JsonSerializer serializer;
        readonly bool migrationModeEnabled;
    }
}