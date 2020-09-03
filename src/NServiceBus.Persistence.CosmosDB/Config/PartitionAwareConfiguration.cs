namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Configuration.AdvancedExtensibility;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    public class PartitionAwareConfiguration : ExposeSettings
    {
        Dictionary<Type, MapUntyped> typeToPartitionMappers = new Dictionary<Type, MapUntyped>();
        Dictionary<Type, string> typeToContainerMappers = new Dictionary<Type, string>();
        Dictionary<Type, string> typeToPartitionKeyPath = new Dictionary<Type, string>();

        internal PartitionAwareConfiguration(PersistenceExtensions<CosmosDbPersistence> persistenceSettings) : base(persistenceSettings.GetSettings())
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="map"></param>
        /// <param name="containerName"></param>
        /// <param name="partitionKeyPath"></param>
        /// <typeparam name="T"></typeparam>
        public void AddPartitionMappingForMessageType<T>(Map<T> map, string containerName, string partitionKeyPath)
        {
            typeToPartitionMappers[typeof(T)] = (headers, messageId, message) => map(headers, messageId, (T)message);
            typeToContainerMappers[typeof(T)] = containerName;
            typeToPartitionKeyPath[typeof(T)] = partitionKeyPath;
        }

        internal string MapMessageToContainer(Type messageType)
        {
            if (!typeToContainerMappers.TryGetValue(messageType, out var containerName))
            {
                throw new Exception($"No container name mapping is found for message type '{messageType}'.");
            }

            return containerName;
        }

        internal string MapMessageToPartitionKeyPath(Type messageType)
        {
            if (!typeToPartitionKeyPath.TryGetValue(messageType, out var partitionKeyPath))
            {
                throw new Exception($"No partition key path mapping is found for message type '{messageType}'.");
            }

            return partitionKeyPath;
        }

        internal PartitionKey MapMessageToPartition(IReadOnlyDictionary<string, string> headers, string messageId, Type messageType, object message)
        {
            if (!typeToPartitionMappers.TryGetValue(messageType, out var mapper))
            {
                throw new Exception($"No partition mapping is found for message type '{messageType}'.");
            }

            var partitionKey = mapper(headers, messageId, message);

            if (partitionKey != PartitionKey.None)
            {
                return partitionKey;
            }
            throw new Exception($"Partition '{partitionKey}' returned by partition mapping of '{messageType}' did not return a result.");
        }
    }
}