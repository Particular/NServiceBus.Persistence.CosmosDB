namespace NServiceBus.Persistence.CosmosDB;

using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Azure.Cosmos;
using Outbox;

sealed class CosmosOutboxTransaction(ContainerHolderResolver resolver, ContextBag context) : IOutboxTransaction
{
    public StorageSession StorageSession { get; } = new(resolver, context);
    public PartitionKey? PartitionKey { get; set; }

    // By default, store and commit are enabled
    public bool AbandonStoreAndCommit { get; set; }

    public Task Commit(CancellationToken cancellationToken = default) =>
        AbandonStoreAndCommit ? Task.CompletedTask : StorageSession.Commit(cancellationToken);

    public void Dispose() => StorageSession.Dispose();

    public ValueTask DisposeAsync() => StorageSession.DisposeAsync();
}