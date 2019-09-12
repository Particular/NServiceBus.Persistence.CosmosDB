namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Sagas;
    using Persistence;

    class SagaPersister : ISagaPersister
    {
        Container container;

        public SagaPersister(CosmosClient cosmosClient)
        {
            container = cosmosClient.GetContainer("mydb", "Sagas");
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var document = WrapInDocument(sagaData, partitionKey);
            return container.CreateItemAsync(document, new PartitionKey(partitionKey));
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            var document = WrapInDocument(sagaData, partitionKey);
            return container.ReplaceItemAsync(document, sagaData.Id.ToString(), new PartitionKey(partitionKey));
        }

        static CosmosDbSagaDocument WrapInDocument(IContainSagaData sagaData, string partitionKey)
        {
            var document = new CosmosDbSagaDocument
            {
                PartitionKey = partitionKey,
                SagaId = sagaData.Id, // same as partitionKey
                SagaType = "sagaType.GetType().FullName", //TODO: incorrect type assigned
                SagaData = sagaData,
                EntityType = sagaData.GetType().FullName,
                Metadata = new CosmosDbSagaMetadata
                {
                    PersisterVersion = "0.0.0.1", // todo: decided how to compute this
                    SagaDataVersion = "0.0.0.1" // todo: decided how to compute this
                }
            };
            return document;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();
            var itemResponse = await container.ReadItemAsync<CosmosDbSagaDocument>(sagaId.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

            return (TSagaData) itemResponse.Resource.SagaData;
        }

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var activeSagaInstance = context.Get<ActiveSagaInstance>();
            var sagaType = activeSagaInstance.Instance.GetType();
            var sagaId = SagaIdGenerator.Generate(sagaType, propertyValue);

            return Get<TSagaData>(sagaId, session, context);
        }

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = sagaData.Id.ToString();
            return container.DeleteItemAsync<dynamic>(sagaData.Id.ToString(), new PartitionKey(partitionKey));
        }
    }
}