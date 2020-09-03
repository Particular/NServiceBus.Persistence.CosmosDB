namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System;
    using NServiceBus.Outbox;

    class OutboxRecord
    {
        public string Id { get; set; }

        public DateTime? Dispatched { get; set; }

        public TransportOperation[] TransportOperations { get; set; }
    }
}
