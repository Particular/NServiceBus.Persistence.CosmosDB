﻿namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Generic;

class StorageTransportOperation
{
    public StorageTransportOperation()
    {
    }

    public StorageTransportOperation(Outbox.TransportOperation source)
    {
        MessageId = source.MessageId;
        Options = source.Options != null ? new Dictionary<string, string>(source.Options) : [];
        Body = source.Body;
        Headers = source.Headers;
    }

    public string MessageId { get; set; }
    public Dictionary<string, string> Options { get; set; }
    public ReadOnlyMemory<byte> Body { get; set; }
    public Dictionary<string, string> Headers { get; set; }


    public Outbox.TransportOperation ToTransportType() =>
        new(MessageId, new Transport.DispatchProperties(Options), Body, Headers);
}