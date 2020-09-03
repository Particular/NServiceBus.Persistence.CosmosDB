namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Outbox;

    class OutboxPersister : IOutboxStorage
    {
        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            return Task.FromResult((OutboxTransaction)new CosmosOutboxTransaction());
        }

        public Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
            //This always must return null for the Outbox "hack" to work
            return null;
        }

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
            var cosmosTransaction = transaction as CosmosOutboxTransaction;

            if (cosmosTransaction.TransactionalBatch is null)
            {
                return Task.CompletedTask;
            }

            //TODO: Probably serialize and add keypath/partitionkey JTokens and id

            cosmosTransaction.TransactionalBatch.CreateItem(new OutboxRecord { Id = message.MessageId, TransportOperations = message.TransportOperations });

            return Task.CompletedTask;
        }

        public Task SetAsDispatched(string messageId, ContextBag context)
        {
            throw new NotImplementedException();
        }
    }
}
