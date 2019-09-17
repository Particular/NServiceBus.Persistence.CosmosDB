namespace NServiceBus.Persistence.CosmosDB.Subscriptions
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Unicast.Subscriptions;

    class EventSubscriptionIdGenerator
    {
        public static Guid Generate(string endpointName, string transportAddress, MessageType messageType)
        {
            return DeterministicGuid($"{endpointName}_{transportAddress}_{messageType}");
        }

        static Guid DeterministicGuid(string src)
        {
            var stringBytes = Encoding.UTF8.GetBytes(src);

            using (var sha1CryptoServiceProvider = new SHA1CryptoServiceProvider())
            {
                var hashedBytes = sha1CryptoServiceProvider.ComputeHash(stringBytes);
                Array.Resize(ref hashedBytes, 16);
                return new Guid(hashedBytes);
            }
        }
    }
}