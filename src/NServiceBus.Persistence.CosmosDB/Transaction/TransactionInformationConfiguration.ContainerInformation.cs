namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;

/// <summary>
/// The configuration options for extracting transaction information.
/// </summary>
public partial class TransactionInformationConfiguration
{
    internal bool HasCustomContainerHeaderExtractors => ContainerInformationExtractor.HasCustomHeaderExtractors;
    internal bool HasCustomContainerMessageExtractors => ContainerInformationExtractor.HasCustomMessageExtractors;

    /// <summary>
    /// Adds an extraction rule that provides the same container information when the given <paramref name="headerKey"/> exists.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="containerInformation">The container information to be used for the specified <paramref name="headerKey"/>.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeader(string headerKey, ContainerInformation containerInformation) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeader(headerKey, containerInformation);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extraction function.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeader(string headerKey, Func<string, ContainerInformation> extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeader(headerKey, extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeader<TArg>(string headerKey, Func<string, TArg, ContainerInformation> extractor, TArg extractorArgument) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeader(headerKey, extractor, extractorArgument);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from the message headers.
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeaders(Func<IReadOnlyDictionary<string, string>, ContainerInformation?> extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeaders(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from the message headers.
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, ContainerInformation?> extractor, TArg extractorArgument) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeaders(extractor, extractorArgument);

    /// <summary>
    /// Adds an instance of <see cref="IContainerInformationFromHeadersExtractor"/> to the list of header extractors.
    /// </summary>
    /// <param name="extractor">The custom extractor.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromHeaders(IContainerInformationFromHeadersExtractor extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromHeaders(extractor);

    /// <summary>
    /// Adds an extraction rule that provides the same container information for a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="containerInformation">The container information to be used for the specified <typeparamref name="TMessage"/>.</param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage<TMessage>(ContainerInformation containerInformation) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage<TMessage>(containerInformation);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage<TMessage>(Func<TMessage, ContainerInformation> extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage<TMessage, TArg>(Func<TMessage, TArg, ContainerInformation> extractor, TArg extractorArgument) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage(extractor, extractorArgument);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage<TMessage>(Func<TMessage, IReadOnlyDictionary<string, string>, ContainerInformation> extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the container information from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, ContainerInformation> extractor, TArg extractorArgument) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage(extractor, extractorArgument);

    /// <summary>
    /// Adds an instance of <see cref="IContainerInformationFromMessagesExtractor"/> to the list of message extractors.
    /// </summary>
    /// <param name="extractor">The custom extractor.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractContainerInformationFromMessage(IContainerInformationFromMessagesExtractor extractor) =>
        ContainerInformationExtractor.ExtractContainerInformationFromMessage(extractor);

    internal ContainerInformationExtractor ContainerInformationExtractor { get; } = new();
}