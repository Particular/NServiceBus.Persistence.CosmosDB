namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using Extensibility;

    abstract class OutboxModification : Modification
    {
        public OutboxRecord Record { get;  }

        protected OutboxModification(OutboxRecord record, ContextBag context) : base(context)
        {
            Record = record;
        }
    }

    class OutboxStore : OutboxModification
    {
        public OutboxStore(OutboxRecord record, ContextBag context) : base(record, context)
        {
        }
    }
}