namespace NServiceBus.TransactionalSession
{
    using System.Collections.Generic;
    using Persistence.CosmosDB;

    sealed class ControlMessageContainerInformationExtractor : IContainerInformationFromHeadersExtractor
    {
        public const string ContainerNameHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerName";

        public const string ContainerPartitionKeyPathHeaderKey =
            "NServiceBus.TxSession.CosmosDB.ContainerPartitionKeyPath";

        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
        {
            if (headers.TryGetValue(ContainerNameHeaderKey, out string containerName)
                && headers.TryGetValue(ContainerPartitionKeyPathHeaderKey, out string partitionKeyPath))
            {
                containerInformation =
                    new ContainerInformation(containerName, new PartitionKeyPath(partitionKeyPath));
                return true;
            }

            containerInformation = null;
            return false;
        }
    }
}