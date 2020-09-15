using Microsoft.Azure.Cosmos;

namespace NServiceBus
{
    /// <summary>
    /// Public interface for providing a CosmosClient via dependency injection
    /// </summary>
    public interface IProvideCosmosClient
    {
        /// <summary>
        /// The provided CosmosClient
        /// </summary>
        CosmosClient Client { get; }
    }
}