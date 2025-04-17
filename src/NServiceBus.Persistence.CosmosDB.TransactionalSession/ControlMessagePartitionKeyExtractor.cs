namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Persistence.CosmosDB;

sealed class ControlMessagePartitionKeyExtractor : IPartitionKeyFromHeadersExtractor
{
    public const string PartitionKeyStringHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKeyString";

    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        if (headers.TryGetValue(PartitionKeyStringHeaderKey, out string partitionKeyString))
        {
            JToken jToken = JArray.Parse(partitionKeyString).First;

            if (jToken.Type == JTokenType.String)
            {
                partitionKey = new PartitionKey(jToken.Value<string>());
            }
            else if (jToken.Type == JTokenType.Boolean)
            {
                partitionKey = new PartitionKey(jToken.Value<bool>());
            }
            else if (jToken.Type == JTokenType.Float)
            {
                partitionKey = new PartitionKey(jToken.Value<double>());
            }
            else
            {
                throw new InvalidOperationException(
                    $"Could not parse the partition key with value '{partitionKeyString}' because the type '{jToken.Type}' was not known.");
            }

            return true;
        }

        partitionKey = null;
        return false;
    }
}