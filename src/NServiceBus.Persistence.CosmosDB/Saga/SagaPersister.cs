namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Sagas;
    using Persistence;

    class SagaPersister : ISagaPersister
    {
        Container container;
        static MethodInfo wrapInDocumentMethod;

        public SagaPersister(CosmosClient cosmosClient)
        {
            container = cosmosClient.GetContainer("mydb", "Sagas");
            wrapInDocumentMethod = GetType().GetMethod(nameof(WrapInDocument), BindingFlags.Static | BindingFlags.NonPublic);
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            return InvokeWrapInDocumentAsGenericMethod(sagaData, context, (document, sagaId, partitionKey) => container.CreateItemAsync(document, partitionKey));
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            return InvokeWrapInDocumentAsGenericMethod(sagaData, context, (document, sagaId, partitionKey) => container.ReplaceItemAsync(document, sagaId, partitionKey));
        }

        static Task InvokeWrapInDocumentAsGenericMethod(IContainSagaData sagaData, ContextBag context, Func<object, string, PartitionKey?, Task<ItemResponse<object>>> operation)
        {
            // Save and Update methods require generic method to be executed - build a generic method
            var sagaDataType = sagaData.GetType();
            var genericMethod = wrapInDocumentMethod.MakeGenericMethod(sagaDataType);

            var partitionKey = sagaData.Id.ToString();
            var document = genericMethod.Invoke(null, new object[] { sagaData, partitionKey, context });
            return operation(document, sagaData.Id.ToString(), new PartitionKey(partitionKey));
        }

        static CosmosDbSagaDocument<TSagaData> WrapInDocument<TSagaData>(IContainSagaData sagaData, string partitionKey, ContextBag context) where TSagaData : IContainSagaData
        {
            var document = new CosmosDbSagaDocument<TSagaData>();
            document.PartitionKey = partitionKey;
            document.SagaId = sagaData.Id;
            document.SagaType = context.GetSagaType().FullName;
            document.SagaData = (TSagaData)sagaData;
            document.EntityType = sagaData.GetType().FullName;
            document.Metadata = new Dictionary<string, string>
            {
                { "PersisterVersion", "0.0.0.1"}, // todo: decided how to compute this
                { "SagaDataVersion", "0.0.0.1"} // todo: decided how to compute this
            };
            return document;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();
            var itemResponse = await container.ReadItemAsync<CosmosDbSagaDocument<TSagaData>>(sagaId.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

            return itemResponse.Resource.SagaData;
        }

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            // Saga ID needs to be calculated the same way as in SagaIdGenerator does
            var sagaType = context.GetSagaType();
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