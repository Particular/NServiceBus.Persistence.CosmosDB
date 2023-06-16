namespace NServiceBus.Persistence.CosmosDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Outbox;
    using Transport;

    class CosmosSynchronizedStorageSession : ICompletableSynchronizedStorageSession, IWorkWithSharedTransactionalBatch
    {
        readonly ContainerHolderResolver containerHolderResolver;
        StorageSession storageSession;
        bool commitOnComplete;
        bool disposed;

        public CosmosSynchronizedStorageSession(ContainerHolderResolver containerHolderResolver) => this.containerHolderResolver = containerHolderResolver;

        public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
            CancellationToken cancellationToken = default)
        {
            if (transaction is CosmosOutboxTransaction cosmosOutboxTransaction)
            {
                storageSession = cosmosOutboxTransaction.StorageSession;
                // because the synchronized storage session acts as decorator that forwards operations to the storage session
                // and we require access to the current context bag we need to make sure to assign the current context bag
                // to the storage session that was created as part of the outbox seam.
                CurrentContextBag = context;
                commitOnComplete = false;
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
            CancellationToken cancellationToken = default) =>
            new ValueTask<bool>(false);

        public Task Open(ContextBag contextBag, CancellationToken cancellationToken = default)
        {
            // Creating the storage session already sets the correct context bag so there is no need to assign
            // CurrentContextBag here
            storageSession = new StorageSession(containerHolderResolver, contextBag);
            commitOnComplete = true;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default) =>
            commitOnComplete ? storageSession.Commit(cancellationToken) : Task.CompletedTask;

        public void Dispose()
        {
            if (!commitOnComplete || disposed)
            {
                return;
            }

            storageSession.Dispose();
            disposed = true;
        }

        public void AddOperation(IOperation operation) => storageSession.AddOperation(operation);

        public ContextBag CurrentContextBag
        {
            get => storageSession.CurrentContextBag;
            set => storageSession.CurrentContextBag = value;
        }

        public Container Container => storageSession.Container;
        public PartitionKeyPath PartitionKeyPath => storageSession.PartitionKeyPath;
    }
}