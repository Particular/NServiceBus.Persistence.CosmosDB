namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Sagas;

    static class ContextBagExtensions
    {
        public static Type GetSagaType(this ContextBag contextBag)
        {
            var activeSagaInstance = contextBag.Get<ActiveSagaInstance>();
            var sagaType = activeSagaInstance.Instance.GetType();

            return sagaType;
        }
    }
}