namespace NServiceBus.Persistence.CosmosDB;

using System;

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

    public string Id { get; set; }

    public bool Dispatched { get; set; }

    public StorageTransportOperation[] TransportOperations { get; set; } = Array.Empty<StorageTransportOperation>();
}