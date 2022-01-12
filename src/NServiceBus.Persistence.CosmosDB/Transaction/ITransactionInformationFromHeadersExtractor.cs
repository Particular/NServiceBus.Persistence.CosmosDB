namespace NServiceBus.Persistence.CosmosDB
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public interface ITransactionInformationFromHeadersExtractor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
    }
}