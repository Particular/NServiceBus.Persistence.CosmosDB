namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Extracts transaction information such as the <see cref="PartitionKey"/> and optionally the <see cref="ContainerInformation"/> from message instances.
    /// </summary>
    public interface ITransactionInformationFromMessagesExtractor
    {
        /// <summary>
        /// Tries to extract a <see cref="PartitionKey"/> and optionally the <see cref="ContainerInformation"/> from message instance.
        /// </summary>
        /// <param name="message">The currently received message instance.</param>
        /// <param name="partitionKey">The <see cref="PartitionKey"/>.</param>
        /// <param name="containerInformation">The optional <see cref="ContainerInformation"/>.</param>
        /// <returns><c>true</c> if the <paramref name="partitionKey"/> is set; otherwise <c>false</c></returns>
        bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
    }
}