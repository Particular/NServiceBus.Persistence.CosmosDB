namespace NServiceBus.Persistence.CosmosDB
{
    using Extensibility;

    // Required for testing
    internal interface IWorkWithSharedTransactionalBatch
    {
        void AddOperation(Operation operation);
        ContextBag CurrentContextBag { get; set; }
    }
}