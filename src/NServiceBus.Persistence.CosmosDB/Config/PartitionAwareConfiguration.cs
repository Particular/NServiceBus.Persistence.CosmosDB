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
        Dictionary<Type, Map> typeToPartitionMappers = new Dictionary<Type, Map>();
        Dictionary<Type, string> typeToContainerMappers = new Dictionary<Type, string>();

        internal PartitionAwareConfiguration(PersistenceExtensions<CosmosDbPersistence> persistenceSettings) : base(persistenceSettings.GetSettings())
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="map"></param>
        /// <param name="containerName"></param>
        /// <typeparam name="T"></typeparam>
        public void AddPartitionMappingForMessageType<T>(Map map, string containerName)
        {
            typeToPartitionMappers[typeof(T)] = (headers, messageId, message) => map(headers, messageId, (T)message);
            typeToContainerMappers[typeof(T)] = containerName;
        }

        internal string MapMessageToContainer(Type messageType)
        {
            if (!typeToContainerMappers.TryGetValue(messageType, out var containerName))
            {
                throw new Exception($"No container name mapping is found for message type '{messageType}'.");
            }

            return containerName;
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