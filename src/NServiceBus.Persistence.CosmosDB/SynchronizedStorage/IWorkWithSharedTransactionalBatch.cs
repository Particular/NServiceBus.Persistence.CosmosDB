﻿namespace NServiceBus.Persistence.CosmosDB;

using Extensibility;
using Microsoft.Azure.Cosmos;

// Required for testing
interface IWorkWithSharedTransactionalBatch
{
    void AddOperation(IOperation operation);
    ContextBag CurrentContextBag { get; set; }
    Container Container { get; }
    PartitionKeyPath PartitionKeyPath { get; }
}