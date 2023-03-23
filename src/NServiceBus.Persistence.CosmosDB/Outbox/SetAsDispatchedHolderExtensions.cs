#nullable enable

namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    static class SetAsDispatchedHolderExtensions
    {
        public static void ThrowIfContainerIsNotSet(this SetAsDispatchedHolder setAsDispatchedHolder)
        {
            if (setAsDispatchedHolder.ContainerHolder != null && setAsDispatchedHolder.ContainerHolder.Container != null)
            {
                return;
            }

            throw new Exception($"For the outbox to work a container must be configured. Either configure a default one using '{nameof(CosmosPersistenceConfig.DefaultContainer)}' or set one via '{nameof(CosmosPersistenceConfig.TransactionInformation)}'.");
        }
    }
}