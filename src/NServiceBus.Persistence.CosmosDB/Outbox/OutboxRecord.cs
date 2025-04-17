namespace NServiceBus.Persistence.CosmosDB;

class OutboxRecord
{
    public OutboxRecord()
    {
    }

    public OutboxRecord(string id, StorageTransportOperation[] transportOperations)
    {
        Id = id;
        TransportOperations = transportOperations;
    }

    public string Id { get; init; }

    public bool Dispatched { get; set; }

    public StorageTransportOperation[] TransportOperations { get; init; } = [];
}