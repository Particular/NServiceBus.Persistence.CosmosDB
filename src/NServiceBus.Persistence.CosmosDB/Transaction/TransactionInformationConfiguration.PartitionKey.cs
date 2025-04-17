namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

/// <summary>
/// The configuration options for extracting transaction information.
/// </summary>
public partial class TransactionInformationConfiguration
{
    /// <summary>
    /// Adds an instance of <see cref="IPartitionKeyFromHeadersExtractor"/> to the list of header extractors.
    /// </summary>
    /// <param name="extractor">The custom extractor.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeaders(IPartitionKeyFromHeadersExtractor extractor) => PartitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, string> extractor) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, string> extractor, TArg extractorArgument) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, extractor, extractorArgument);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeader(string headerKey) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from headers.
    /// </summary>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The extractor argument type.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeaders<TArg>(Func<IReadOnlyDictionary<string, string>, TArg, PartitionKey?> extractor, TArg extractorArgument) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor, extractorArgument);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from headers.
    /// </summary>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeaders(Func<IReadOnlyDictionary<string, string>, PartitionKey?> extractor) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeaders(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeader(string headerKey, Func<string, PartitionKey> extractor) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given header represented by <paramref name="headerKey"/>.
    /// </summary>
    /// <param name="headerKey">The header key.</param>
    /// <param name="extractor">The extractor function to extract the header value.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TArg">The argument type.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromHeader<TArg>(string headerKey, Func<string, TArg, PartitionKey> extractor, TArg extractorArgument) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromHeader(headerKey, extractor, extractorArgument);

    /// <summary>
    /// Adds an instance of <see cref="IPartitionKeyFromMessageExtractor"/> to the list of header extractors.
    /// </summary>
    /// <param name="extractor">The custom extractor.</param>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromMessages(IPartitionKeyFromMessageExtractor extractor) => PartitionKeyExtractor.ExtractPartitionKeyFromMessages(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, PartitionKey> extractor) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <typeparam name="TArg">The argument passed as state to the <paramref name="extractor"/></typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, TArg, PartitionKey> extractor, TArg extractorArgument) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor, extractorArgument);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromMessage<TMessage>(Func<TMessage, IReadOnlyDictionary<string, string>, PartitionKey> extractor) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor);

    /// <summary>
    /// Adds an extraction rule that extracts the partition key from a given message type <typeparamref name="TMessage"/>
    /// </summary>
    /// <param name="extractor">The extraction function.</param>
    /// <param name="extractorArgument">The argument passed as state to the <paramref name="extractor"/></param>
    /// <typeparam name="TMessage">The message type to match against.</typeparam>
    /// <typeparam name="TArg">The argument passed as state to the <paramref name="extractor"/></typeparam>
    /// <remarks>Explicitly added extractors and extraction rules are executed before extractors registered on the container.</remarks>
    public void ExtractPartitionKeyFromMessage<TMessage, TArg>(Func<TMessage, IReadOnlyDictionary<string, string>, TArg, PartitionKey> extractor, TArg extractorArgument) =>
        PartitionKeyExtractor.ExtractPartitionKeyFromMessage(extractor, extractorArgument);

    internal PartitionKeyExtractor PartitionKeyExtractor { get; } = new();
}