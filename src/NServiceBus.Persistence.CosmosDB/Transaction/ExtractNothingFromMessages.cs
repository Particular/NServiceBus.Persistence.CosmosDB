namespace NServiceBus.Persistence.CosmosDB
{
    using Microsoft.Azure.Cosmos;

    class ExtractNothingFromMessages : IExtractTransactionInformationFromMessages
    {
        public bool TryExtract(object message, out PartitionKey? partitionKey, out ContainerInformation? containerInformation)
        {
            partitionKey = null;
            containerInformation = null;
            return false;
        }
    }
}