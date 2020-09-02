﻿namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    ///
    /// </summary>
    /// <param name="headers"></param>
    /// <param name="messageId"></param>
    /// <param name="message"></param>
    public delegate PartitionKey Map<in T>(IReadOnlyDictionary<string, string> headers, string messageId, T message);

    delegate PartitionKey MapUntyped(IReadOnlyDictionary<string, string> headers, string messageId, object message);
}