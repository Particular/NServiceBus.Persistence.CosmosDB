﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Sagas;

    class SagaPersister : ISagaPersister
    {
        public SagaPersister(ContainerHolder containerHolder, JsonSerializer serializer)
        {
            this.containerHolder = containerHolder;
            this.serializer = serializer;
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaSave(sagaData, correlationProperty, partitionKey, containerHolder.PartitionKeyPath, serializer));
            return Task.CompletedTask;
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var storageSession = (StorageSession)session;
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaUpdate(sagaData, partitionKey, containerHolder.PartitionKeyPath, serializer, context));
            return Task.CompletedTask;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var storageSession = (StorageSession)session;

            // reads need to go directly
            var container = storageSession.Container;
            var partitionKey = GetPartitionKey(context, sagaId);

            var responseMessage = await container.ReadItemStreamAsync(sagaId.ToString(), partitionKey).ConfigureAwait(false);

            if (responseMessage.StatusCode == HttpStatusCode.NotFound || responseMessage.Content == null)
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
            var partitionKey = GetPartitionKey(context, sagaData.Id);

            storageSession.AddOperation(new SagaDelete(sagaData, partitionKey, containerHolder.PartitionKeyPath, context));

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

        readonly ContainerHolder containerHolder;
        JsonSerializer serializer;

        internal static readonly string SchemaVersion = "1.0.0";
    }
}