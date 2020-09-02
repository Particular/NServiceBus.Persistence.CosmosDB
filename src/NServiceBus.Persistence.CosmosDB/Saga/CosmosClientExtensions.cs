namespace NServiceBus.Features
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Sagas;
    using Persistence.CosmosDB;

    internal static class CosmosClientExtensions
    {
        public static async Task PopulateContainers(this CosmosClient cosmosClient, string databaseName, SagaMetadataCollection sagaMetadataCollection, PartitionAwareConfiguration partitionAwareConfiguration, bool cheat = false)
        {
            var database = cosmosClient.GetDatabase(databaseName);
            foreach (var sagaMetadata in sagaMetadataCollection)
            {
                ContainerProperties containerProperties = null;
                string containerName = null, partitionKeyPath = null;
                foreach (var associatedMessage in sagaMetadata.AssociatedMessages.Where(m => m.IsAllowedToStartSaga))
                {
                    containerName = partitionAwareConfiguration.MapMessageToContainer(cheat ? typeof(object) : associatedMessage.MessageType);
                    partitionKeyPath = partitionAwareConfiguration.MapMessageToPartitionKeyPath(cheat ? typeof(object) : associatedMessage.MessageType);
                    var container = database.GetContainer(containerName);
                    try
                    {
                        var response = await container.ReadContainerAsync().ConfigureAwait(false);
                        containerProperties = response.Resource;
                        // currently not checking if things are actually coherent. We probably should
                        if (containerProperties != null)
                        {
                            break;
                        }
                    }
                    catch (CosmosException)
                    {
                        break;
                    }

                }

                if (containerProperties == null)
                {
                    containerProperties = new ContainerProperties(containerName, partitionKeyPath);
                }

                if (sagaMetadata.TryGetCorrelationProperty(out var property) && property.Name != "Id")
                {
                    // cannot be longer than 60 chars! Need to figure out a unique way
                    containerProperties.UniqueKeyPolicy.UniqueKeys.Add(new UniqueKey
                    {
                        Paths = {$"/{property.Name}"}
                    });
                }

                await database.CreateContainerIfNotExistsAsync(containerProperties).ConfigureAwait(false);
            }
        }
    }
}