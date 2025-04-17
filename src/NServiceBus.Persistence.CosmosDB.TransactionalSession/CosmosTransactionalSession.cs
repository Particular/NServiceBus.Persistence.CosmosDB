namespace NServiceBus.TransactionalSession;

using Persistence.CosmosDB;

sealed class CosmosTransactionalSession : TransactionalSession
{
    public CosmosTransactionalSession() =>
        Defaults(s =>
        {
            s.GetOrCreate<TransactionInformationConfiguration>().ExtractPartitionKeyFromHeaders(new ControlMessagePartitionKeyExtractor());
            s.GetOrCreate<TransactionInformationConfiguration>().ExtractContainerInformationFromHeaders(new ControlMessageContainerInformationExtractor());
        });
}