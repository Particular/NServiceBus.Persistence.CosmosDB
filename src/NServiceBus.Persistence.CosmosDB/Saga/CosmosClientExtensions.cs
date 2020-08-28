namespace NServiceBus.Features
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Sagas;

    internal static class CosmosClientExtensions
    {
        public static async Task PopulateContainers(this CosmosClient cosmosClient, string databaseName, SagaMetadataCollection sagaMetadataCollection)
        {
            var database = cosmosClient.GetDatabase(databaseName);
            foreach (var sagaMetadata in sagaMetadataCollection)
            {
                // TODO: make configurable
                var containerName = sagaMetadata.SagaEntityType.Name;
                var containerProperties = new ContainerProperties(containerName, "/Id");

                if (sagaMetadata.TryGetCorrelationProperty(out var property) && property.Name != "Id")
                {
                    containerProperties.UniqueKeyPolicy = new UniqueKeyPolicy
                    {
                        UniqueKeys =
                        {
                            new UniqueKey
                            {
                                Paths = {$"/{property.Name}"}
                            }
                        }
                    };
                }

                await database.CreateContainerIfNotExistsAsync(containerProperties).ConfigureAwait(false);
            }
        }
    }
}