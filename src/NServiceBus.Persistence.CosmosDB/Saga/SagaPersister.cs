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
            // TODO: Is this the right way to access saga metadata?
            var partitionKey = SagaIdGenerator.Generate(context.Get<SagaMetadata>().SagaEntityType, correlationProperty.Value);
            return container.CreateItemAsync(sagaData, new PartitionKey(partitionKey.ToString()));
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var partitionKey = SagaIdGenerator.Generate(context.Get<SagaMetadata>().SagaEntityType, /*correlationProperty.Value*/"");
            return container.ReplaceItemAsync(sagaData, sagaData.Id.ToString(), new PartitionKey(partitionKey.ToString()));
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var sagaEntityType = typeof(TSagaData);
            var partitionKey = SagaIdGenerator.Generate(sagaEntityType, /*correlationProperty.Value*/"");
            var itemResponse = await container.ReadItemAsync<TSagaData>(sagaId.ToString(), new PartitionKey(partitionKey.ToString())).ConfigureAwait(false);

            return itemResponse.Resource;
        }

        public async Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var sagaEntityType = typeof(TSagaData);
            var partitionKey = SagaIdGenerator.Generate(sagaEntityType, propertyValue);
            var itemResponse = await container.ReadItemAsync<TSagaData>(propertyValue.ToString(), new PartitionKey(partitionKey.ToString())).ConfigureAwait(false);

            return itemResponse.Resource;
        }

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            var sagaEntityType = context.Get<SagaMetadata>().SagaEntityType;
            var partitionKey = SagaIdGenerator.Generate(sagaEntityType, /*correlationProperty.Value*/"");
            return container.DeleteItemAsync<dynamic>(sagaData.Id.ToString(), new PartitionKey(partitionKey.ToString()));
        }
    }
}