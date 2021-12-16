namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Sagas;

    class SagaPersister : ISagaPersister
    {

        //TODO: pass in the pessimistic locking settings
        public SagaPersister(JsonSerializer serializer, bool migrationModeEnabled)
        {
            this.serializer = serializer;
            this.migrationModeEnabled = migrationModeEnabled;
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaSave(sagaData, partitionKey, serializer, context));
            return Task.CompletedTask;
        }

        public Task Update(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaUpdate(sagaData, partitionKey, serializer, context));
            return Task.CompletedTask;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // reads need to go directly
            var container = storageSession.ContainerHolder.Container;
            var partitionKey = GetPartitionKey(context, sagaId);

            using (var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var sagaStream = responseMessage.Content;

                var sagaNotFound = responseMessage.StatusCode == HttpStatusCode.NotFound || sagaStream == null;

                // if the previous lookup by id wasn't successful and the migration mode is enabled try to query for the saga data because the saga id probably represents
                // the saga id of the migrated saga.
                if (sagaNotFound && migrationModeEnabled)
                {
                    var query = $@"SELECT TOP 1 * FROM c WHERE c[""{MetadataExtensions.MetadataKey}""][""{MetadataExtensions.SagaDataContainerMigratedSagaIdMetadataKey}""] = '{sagaId}'";
                    var queryDefinition = new QueryDefinition(query);
                    var queryStreamIterator = container.GetItemQueryStreamIterator(queryDefinition);

                    using (var iteratorResponse = await queryStreamIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false))
                    {
                        iteratorResponse.EnsureSuccessStatusCode();

                        using (var streamReader = new StreamReader(iteratorResponse.Content))
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            var iteratorResult = await JObject.LoadAsync(jsonReader, cancellationToken).ConfigureAwait(false);

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

                return sagaNotFound ? default : ReadSagaFromStream<TSagaData>(container, context, sagaStream, responseMessage);
            }
        }

        public async Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var sagaId = CosmosSagaIdGenerator.Generate(typeof(TSagaData), propertyName, propertyValue);

            // reads need to go directly
            var container = storageSession.ContainerHolder.Container;
            var partitionKey = GetPartitionKey(context, sagaId);

            using (var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var sagaStream = responseMessage.Content;

                var sagaNotFound = responseMessage.StatusCode == HttpStatusCode.NotFound || sagaStream == null;

                return sagaNotFound ? default : ReadSagaFromStream<TSagaData>(container, context, sagaStream, responseMessage);
            }
        }

        //TODO: change parameters, you will need access to Container
        TSagaData ReadSagaFromStream<TSagaData>(Container container, ContextBag context, Stream sagaStream, ResponseMessage responseMessage) where TSagaData : class, IContainSagaData
        {
            using (sagaStream)
            using (var streamReader = new StreamReader(sagaStream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                //change to deserialize to JOBject instead if pessimistic locking is enabled?
                var sagaData = serializer.Deserialize<TSagaData>(jsonReader);

                // if pessimistic locking is turned on
                // look at the saga metadata in the JObject, if the lease time is not in the past throw an exception
                // update the saga record (adding to the metadata object that we add. Use the current time + lock time as the lease value (LeaseUntil) 
                var response = container.ReplaceItemStreamAsync(null, "", PartitionKey.None, new ItemRequestOptions { IfMatchEtag = responseMessage.Headers.ETag }).GetAwaiter().GetResult();


                context.Set($"cosmos_etag:{sagaData.Id}", responseMessage.Headers.ETag);

                return sagaData;
            }
        }

        public Task Complete(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
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