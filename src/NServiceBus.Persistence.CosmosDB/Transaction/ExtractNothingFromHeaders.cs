namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    class ExtractNothingFromHeaders : IExtractTransactionInformationFromHeaders
    {
        public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
            out ContainerInformation? containerInformation)
        {
            partitionKey = null;
            containerInformation = null;
            return false;
        }
    }
}