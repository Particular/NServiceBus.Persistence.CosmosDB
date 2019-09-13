namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using System.Net;
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

        public SagaPersister(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            container = cosmosClient.GetContainer(databaseName, containerName);
            wrapInDocumentMethod = GetType().GetMethod(nameof(WrapInDocument), BindingFlags.Static | BindingFlags.NonPublic);
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            return InvokeWrapInDocumentAsGenericMethod(sagaData, context, (document, sagaId, partitionKey) => container.CreateItemAsync(document, partitionKey));
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            return InvokeWrapInDocumentAsGenericMethod(sagaData, context, (document, sagaId, partitionKey) =>
            {
                // only delete if we have the same version as in CosmosDB
                context.TryGet<string>("etag", out var etag);
                var options = new ItemRequestOptions { IfMatchEtag = etag };

                return container.ReplaceItemAsync(document, sagaId, partitionKey, options);
            });
        }

        static async Task InvokeWrapInDocumentAsGenericMethod(IContainSagaData sagaData, ContextBag context, Func<object, string, PartitionKey?, Task<ItemResponse<object>>> operation)
        {
            // Save and Update methods require generic method to be executed - build a generic method
            var sagaDataType = sagaData.GetType();
            var genericMethod = wrapInDocumentMethod.MakeGenericMethod(sagaDataType);

            var partitionKey = sagaData.Id.ToString();
            var document = genericMethod.Invoke(null, new object[] { sagaData, partitionKey, context });
            var response = await operation(document, sagaData.Id.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

            context.Set("etag", response.ETag);
        }

        static CosmosDbSagaDocument<TSagaData> WrapInDocument<TSagaData>(IContainSagaData sagaData, string partitionKey, ContextBag context) where TSagaData : IContainSagaData
        {
            var sagaType = context.GetSagaType();
            var sagaDataType = sagaData.GetType();

            var document = new CosmosDbSagaDocument<TSagaData>();
            document.PartitionKey = partitionKey;
            document.SagaId = sagaData.Id;
            document.SagaData = (TSagaData)sagaData;
            document.Metadata = new Dictionary<string, string>
            {
                { "PersisterVersion", FileVersionRetriever.GetFileVersion(typeof(SagaPersister))},
                { "SagaType", sagaType.FullName }, 
                { "SagaTypeVersion",  FileVersionRetriever.GetFileVersion(sagaType)},
                { "SagaDataType",  sagaDataType.FullName},
                { "SagaDataTypeVersion",  FileVersionRetriever.GetFileVersion(sagaDataType)}
            };

            return document;
        }

        public async Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            var partitionKey = sagaId.ToString();
            try
            {
                var itemResponse = await container.ReadItemAsync<CosmosDbSagaDocument<TSagaData>>(sagaId.ToString(), new PartitionKey(partitionKey)).ConfigureAwait(false);

                context.Set("etag", itemResponse.ETag);

                return itemResponse.Resource.SagaData;
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
            context.TryGet<string>("etag", out var etag);
            var options = new ItemRequestOptions { IfMatchEtag = etag };

            return container.DeleteItemAsync<dynamic>(sagaData.Id.ToString(), new PartitionKey(partitionKey), options);
        }
    }

}