namespace NServiceBus.TransactionalSession;

using Microsoft.Azure.Cosmos;

/// <summary>
/// The options allowing to control the behavior of the transactional session.
/// </summary>
public sealed class CosmosOpenSessionOptions : OpenSessionOptions
{
    /// <summary>
    /// Creates a new instance of the CosmosOpenSessionOptions.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="containerInformation">The optional container information.</param>
    public CosmosOpenSessionOptions(PartitionKey partitionKey, ContainerInformation? containerInformation = null)
    {
        Extensions.Set(partitionKey);
        Metadata.Add(ControlMessagePartitionKeyExtractor.PartitionKeyStringHeaderKey, partitionKey.ToString());

        SetContainerInformationIfRequired(containerInformation);
    }

    internal void SetContainerInformationIfRequired(ContainerInformation? containerInformation)
    {
        if (!containerInformation.HasValue || Extensions.TryGet<ContainerInformation>(out _))
        {
            return;
        }

        Extensions.Set(containerInformation.Value);
        Metadata.Add(ControlMessageContainerInformationExtractor.ContainerNameHeaderKey, containerInformation.Value.ContainerName);
        Metadata.Add(ControlMessageContainerInformationExtractor.ContainerPartitionKeyPathHeaderKey,
            containerInformation.Value.PartitionKeyPath);
    }
}