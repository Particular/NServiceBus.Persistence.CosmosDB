namespace Particular.AzureStoragePersistenceSagaExporter
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    static class SagaIdGenerator
    {
        public static Guid Generate(string sagaEntityType, string correlationPropertyName, string correlationPropertyValue)
        {
            return DeterministicGuid($"{sagaEntityType}_{correlationPropertyName}_{correlationPropertyValue}");
        }

        static Guid DeterministicGuid(string src)
        {
            var stringBytes = Encoding.UTF8.GetBytes(src);

            using var sha1CryptoServiceProvider = new SHA1CryptoServiceProvider();
            var hashedBytes = sha1CryptoServiceProvider.ComputeHash(stringBytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes);
        }
    }
}