namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;

    /// <summary>
    /// Extracts the <see cref="ContainerInformation"/> from the headers at the physical stage when the message has not been deserialized
    /// and the outbox mechanism hasn't started yet. This is the earliest possible point to
    /// extract the <see cref="ContainerInformation"/> in cases the container information doesn't need to be extracted from the message instance.
    /// Otherwise consider using <see cref="IContainerInformationFromMessagesExtractor"/> instead.
    /// </summary>
    public interface IContainerInformationFromHeadersExtractor
    {
        /// <summary>
        /// Tries to extract a <see cref="ContainerInformation"/> from the headers.
        /// </summary>
        /// <param name="headers">The headers of the currently received message.</param>
        /// <param name="containerInformation">The <see cref="ContainerInformation"/>.</param>
        /// <returns><c>true</c> if the <paramref name="containerInformation"/> is set; otherwise <c>false</c></returns>
        bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation);
    }
}