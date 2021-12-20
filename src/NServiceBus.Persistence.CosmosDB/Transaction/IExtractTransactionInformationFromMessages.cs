namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// 
    /// </summary>
    public interface IExtractTransactionInformationFromMessages
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="partitionKey"></param>
        /// <param name="containerInformation"></param>
        /// <returns></returns>
        bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation);
    }
}