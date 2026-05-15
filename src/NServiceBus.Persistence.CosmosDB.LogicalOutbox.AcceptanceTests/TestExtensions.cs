namespace NServiceBus.AcceptanceTests;

using Persistence.CosmosDB;

static class TestExtensions
{
    public static void RegisterFaultyPartitionKeyExtractor(this TransactionInformationConfiguration transactionInformation) =>
        transactionInformation.ExtractPartitionKeyFromMessages(new FaultyPartitionKeyProvider());

    public static void RegisterFaultyContainerInformationExtractor(this TransactionInformationConfiguration transactionInformation) =>
        transactionInformation.ExtractContainerInformationFromMessage(new FaultyContainerInformationProvider());
}