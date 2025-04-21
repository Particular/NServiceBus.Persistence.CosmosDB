namespace NServiceBus.Persistence.CosmosDB;

using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

/// <summary>
/// Extracts the <see cref="PartitionKey"/> from the headers at the physical stage when the message has not been deserialized
/// and the outbox mechanism hasn't started yet. This is the earliest possible point to
/// extract the <see cref="PartitionKey"/> in cases the partition key doesn't need to be extracted from the message instance.
/// Otherwise consider using <see cref="IPartitionKeyFromMessageExtractor"/> instead.
/// </summary>
public interface IPartitionKeyFromHeadersExtractor
{
    /// <summary>
    /// Tries to extract a <see cref="PartitionKey"/> from the headers.
    /// </summary>
    /// <param name="headers">The headers of the currently received message.</param>
    /// <param name="partitionKey">The <see cref="PartitionKey"/>.</param>
    /// <returns><c>true</c> if the <paramref name="partitionKey"/> is set; otherwise <c>false</c></returns>
    bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey);
}