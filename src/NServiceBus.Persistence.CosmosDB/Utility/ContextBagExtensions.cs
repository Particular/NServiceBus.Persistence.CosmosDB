using Microsoft.Azure.Cosmos;
using NServiceBus.Extensibility;

namespace NServiceBus.Persistence.CosmosDB
{
    static class ContextBagExtensions
    {
        public static PartitionKey GetPartitionKey(this ContextBag contextBag)
        {
            return contextBag.Get<PartitionKey>();
        }
    }
}
