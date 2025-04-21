namespace NServiceBus;

using System;

/// <summary>
/// Represents the container name and the partition key path when the container information is provided at runtime through the pipeline.
/// </summary>
public readonly struct ContainerInformation
{
    /// <summary>
    /// Initializes the container information with a specified container name and a corresponding partition key path.
    /// </summary>
    /// <param name="containerName">The name of the container to use.</param>
    /// <param name="partitionKeyPath">The partition key path matching the container in use.</param>
    public ContainerInformation(string containerName, PartitionKeyPath partitionKeyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        ContainerName = containerName;
        PartitionKeyPath = partitionKeyPath;
    }

    /// <summary>
    /// The name of the container to be used.
    /// </summary>
    public string ContainerName { get; }

    /// <summary>
    /// The partition key path that matches the container in use.
    /// </summary>
    public PartitionKeyPath PartitionKeyPath { get; }
}