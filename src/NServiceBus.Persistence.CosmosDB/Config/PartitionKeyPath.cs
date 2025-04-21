namespace NServiceBus;

/// <summary>
/// Represents a partition key path within a container.
/// </summary>
public readonly struct PartitionKeyPath
{
    readonly string partitionKeyPath;

    /// <summary>
    /// Initializes a new partition key path.
    /// </summary>
    /// <param name="partitionKeyPath">The partition key path.</param>\
    /// <remarks>No validation is done to assert whether the provided path matches the Cosmos DB conventions.</remarks>
    public PartitionKeyPath(string partitionKeyPath) => this.partitionKeyPath = partitionKeyPath;

    /// <summary>
    /// Implicitly converts the partition key path into a string.
    /// </summary>
    public static implicit operator string(PartitionKeyPath path) => path.partitionKeyPath;

    /// <inheritdoc />
    public override string ToString() => partitionKeyPath;

    /// <summary>
    /// Compares two partition key paths.
    /// </summary>
    public bool Equals(PartitionKeyPath other) => partitionKeyPath == other.partitionKeyPath;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is PartitionKeyPath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => partitionKeyPath != null ? partitionKeyPath.GetHashCode() : 0;

    /// <summary>
    /// Compares to partition key paths for equality.
    /// </summary>
    public static bool operator ==(PartitionKeyPath left, PartitionKeyPath right) => left.Equals(right);

    /// <summary>
    /// Compares two partition key path for inequality.
    /// </summary>
    public static bool operator !=(PartitionKeyPath left, PartitionKeyPath right) => !left.Equals(right);
}