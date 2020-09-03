namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.Outbox;

    class CosmosOutboxTransaction : OutboxTransaction
    {
        internal TransactionalBatchDecorator TransactionalBatch { get; set; }

        public Task Commit()
        {
            if (TransactionalBatch is null)
            {
                return Task.CompletedTask;
            }

            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
