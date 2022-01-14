namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Extracts the <see cref="PartitionKey"/> from message instances at the logical stage right after the payload has been deserialized.
    /// Implementors of this interface have access to the message instance as well as the headers of the message instance.
    /// The outbox mechanism might already have started and thus providing the <see cref="PartitionKey"/> at this stage corresponds to the logical outbox phase.
    /// In cases when only headers are required use <see cref="IPartitionKeyFromHeadersExtractor"/> instead.
    /// </summary>
    public interface IPartitionKeyFromMessageExtractor
    {
        /// <summary>
        /// Tries to extract a <see cref="PartitionKey"/> from a message instance.
        /// </summary>
        /// <param name="message">The currently received message instance.</param>
        /// <param name="headers">The headers of the currently received message.</param>
        /// <param name="partitionKey">The <see cref="PartitionKey"/>.</param>
        /// <returns><c>true</c> if the <paramref name="partitionKey"/> is set; otherwise <c>false</c></returns>
        bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey);
    }
}