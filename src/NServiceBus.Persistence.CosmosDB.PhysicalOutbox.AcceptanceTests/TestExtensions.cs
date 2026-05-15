namespace NServiceBus.AcceptanceTests;

using Persistence.CosmosDB;

static class TestExtensions
{
    public static void RegisterFaultyPartitionKeyExtractor(this TransactionInformationConfiguration transactionInformation) =>
        transactionInformation.ExtractPartitionKeyFromHeaders(new FaultyPartitionKeyProvider());

    public static void RegisterFaultyContainerInformationExtractor(this TransactionInformationConfiguration transactionInformation) =>
        transactionInformation.ExtractContainerInformationFromHeaders(new FaultyContainerInformationProvider());
}