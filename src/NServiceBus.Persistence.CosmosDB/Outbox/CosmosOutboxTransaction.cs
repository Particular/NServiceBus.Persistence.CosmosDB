namespace NServiceBus.Persistence.CosmosDB.Outbox
{
    using System.Threading.Tasks;
    using NServiceBus.Outbox;

    class CosmosOutboxTransaction : OutboxTransaction
    {
        public StorageSession StorageSession { get; set; }
        
        public Task Commit()
        {
            return StorageSession.Commit();
        }

        public void Dispose()
        {
            StorageSession.Dispose();
        }
    }
}