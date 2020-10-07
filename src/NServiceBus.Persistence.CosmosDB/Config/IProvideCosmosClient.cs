namespace NServiceBus
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Provides a CosmosClient via dependency injection. A custom implementation can be registered on the container and will be picked up by the persistence.
    /// <remarks>     
    /// The client provided will not be disposed by the persistence. It is the responsibility of the provider to take care of proper resource disposal if necessary.
    /// </remarks>
    /// </summary>
    public interface IProvideCosmosClient
    {
        /// <summary>
        /// The CosmosClient to use.
        /// </summary>
        CosmosClient Client { get; }
    }
}
