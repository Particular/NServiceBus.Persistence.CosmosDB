namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Provides a CosmosClient via dependency injection
    /// </summary>
    public interface IProvideCosmosClient
    {
        /// <summary>
        /// The provided CosmosClient
        /// </summary>
        CosmosClient Client { get; }
    }
}