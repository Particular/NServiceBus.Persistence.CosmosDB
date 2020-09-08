namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;

    abstract class Modification
    {
        protected Modification(ContextBag context)
        {
            Context = context;
        }

        public ContextBag Context { get; }

        public virtual void Success(TransactionalBatchOperationResult result)
        {
        }

        public virtual void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception("Concurrency conflict.");
        }
    }
}