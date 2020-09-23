namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Text;
    using FastHashes;
    using Newtonsoft.Json;

    static class CosmosDBSagaIdGenerator
    {
        public static Guid Generate(Type sagaEntityType, string correlationPropertyName, object correlationPropertyValue) => Generate(sagaEntityType.FullName, correlationPropertyName, correlationPropertyValue);

        public static Guid Generate(string sagaEntityTypeFullName, string correlationPropertyName, object correlationPropertyValue)
        {
            // assumes single correlated sagas since v6 doesn't allow more than one corr prop
            // will still have to use a GUID since moving to a string id will have to wait since its a breaking change
            var serializedPropertyValue = JsonConvert.SerializeObject(correlationPropertyValue);
            return DeterministicGuid($"{sagaEntityTypeFullName}_{correlationPropertyName}_{serializedPropertyValue}");
        }

        static Guid DeterministicGuid(string src)
        {
            var stringBytes = Encoding.UTF8.GetBytes(src);

            var hashedBytes = hashProvider.ComputeHash(stringBytes);
            return new Guid(hashedBytes);
        }

        static readonly FarmHash128 hashProvider = new FarmHash128(ulong.MinValue);
    }
}
