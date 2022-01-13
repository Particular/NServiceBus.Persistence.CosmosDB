namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Extracts transaction information such as the <see cref="PartitionKey"/> and optionally the <see cref="ContainerInformation"/> from the headers.
    /// </summary>
    public interface ITransactionInformationFromHeadersExtractor
    {
        /// <summary>
        /// Tries to extract a <see cref="PartitionKey"/> and optionally the <see cref="ContainerInformation"/> from the headers.
        /// </summary>
        /// <param name="headers">The headers of the currently received message.</param>
        /// <param name="partitionKey">The <see cref="PartitionKey"/>.</param>
        /// <param name="containerInformation">The optional <see cref="ContainerInformation"/>.</param>
        /// <returns><c>true</c> if the <paramref name="partitionKey"/> is set; otherwise <c>false</c></returns>
        bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
    }
}