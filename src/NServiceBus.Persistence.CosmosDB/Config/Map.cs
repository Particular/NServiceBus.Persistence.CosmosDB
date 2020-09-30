namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    public delegate PartitionKey Map<in T>(IReadOnlyDictionary<string, string> headers, string messageId, T message);

    delegate PartitionKey MapUntyped(IReadOnlyDictionary<string, string> headers, string messageId, object message);
}