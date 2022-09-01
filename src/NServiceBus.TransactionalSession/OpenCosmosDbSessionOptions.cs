namespace NServiceBus.Persistence.CosmosDB.TransactionalSession;

using Microsoft.Azure.Cosmos;
using NServiceBus.TransactionalSession;

/// <summary>
///
/// </summary>
public class OpenCosmosDbSessionOptions : OpenSessionOptions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <param name="containerInformation"></param>
    public OpenCosmosDbSessionOptions(PartitionKey partitionKey, ContainerInformation? containerInformation = null)
    {
        Extensions.Set(partitionKey);
        Metadata.Add(CosmosControlMessageBehavior.PartitionKeyStringHeaderKey, partitionKey.ToString());

        if (containerInformation == null)
        {
            return;
        }

        Extensions.Set(containerInformation);
        Metadata.Add(CosmosControlMessageBehavior.ContainerNameHeaderKey, containerInformation.Value.ContainerName);
        Metadata.Add(CosmosControlMessageBehavior.ContainerPartitionKeyPathHeaderKey,
            containerInformation.Value.PartitionKeyPath);
    }
}