namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Outbox;

    class OutboxRecord
    {
        public string Id { get; set; }

        public bool Dispatched { get; set; }

        public TransportOperation[] TransportOperations { get; set; } = Array.Empty<TransportOperation>();
    }
}