namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Extracts the <see cref="ContainerInformation"/> from message instances at the logical stage right after the payload has been deserialized.
    /// Implementors of this interface have access to the message instance as well as the headers of the message instance.
    /// The outbox mechanism might already have started and thus providing the <see cref="ContainerInformation"/> at this stage corresponds to the logical outbox phase.
    /// In cases when only headers are required use <see cref="IContainerInformationFromHeadersExtractor"/> instead.
    /// </summary>
    public interface IContainerInformationFromMessagesExtractor
    {
        /// <summary>
        /// Tries to extract a <see cref="PartitionKey"/> from a message instance.
        /// </summary>
        /// <param name="message">The currently received message instance.</param>
        /// <param name="headers">The headers of the currently received message.</param>
        /// <param name="containerInformation">The <see cref="ContainerInformation"/>.</param>
        /// <returns><c>true</c> if the <paramref name="containerInformation"/> is set; otherwise <c>false</c></returns>
        bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation);
    }
}