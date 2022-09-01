namespace NServiceBus.Persistence.CosmosDB.TransactionalSession;

using System;
using NServiceBus.TransactionalSession;

sealed class TransactionalSessionExtension : ITransactionalSessionExtension, IDisposable
{
    readonly CurrentSharedTransactionalBatchHolder.Scope scope;

    public TransactionalSessionExtension(CurrentSharedTransactionalBatchHolder holder) => scope = holder.CreateScope();

    public void Dispose() => scope.Dispose();
}