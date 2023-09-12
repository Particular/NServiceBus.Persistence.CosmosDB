namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json;

    static class CosmosSagaIdGenerator
    {
        const byte SeparatorByte = 95; // Unicode for "_"

        public static Guid Generate(Type sagaEntityType, string correlationPropertyName, object correlationPropertyValue) => Generate(sagaEntityType.FullName, correlationPropertyName, correlationPropertyValue);

        public static Guid Generate(string sagaEntityTypeFullName, string correlationPropertyName, object correlationPropertyValue)
        {
            // assumes single correlated sagas since v6 doesn't allow more than one corr prop
            // will still have to use a GUID since moving to a string id will have to wait since its a breaking change
            var serializedPropertyValue = JsonConvert.SerializeObject(correlationPropertyValue);
            return DeterministicGuid(sagaEntityTypeFullName, correlationPropertyName, serializedPropertyValue);
        }

        [SkipLocalsInit]
        static Guid DeterministicGuid(string sagaEntityTypeFullName, string correlationPropertyName, string serializedPropertyValue)
        {
            // sagaEntityTypeFullName_correlationPropertyName_serializedPropertyValue
            var length = sagaEntityTypeFullName.Length + correlationPropertyName.Length + serializedPropertyValue.Length + 2;

            const int MaxStackLimit = 256;
            var encoding = Encoding.UTF8;
            var maxByteCount = encoding.GetMaxByteCount(length);

            byte[] sharedBuffer = null;
            var stringBufferSpan = maxByteCount <= MaxStackLimit ?
                stackalloc byte[MaxStackLimit] :
                sharedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

            try
            {
                // sagaEntityTypeFullName_
                var numberOfBytesWritten = encoding.GetBytes(sagaEntityTypeFullName.AsSpan(), stringBufferSpan);
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

                // sagaEntityTypeFullName_correlationPropertyName_
                numberOfBytesWritten += encoding.GetBytes(correlationPropertyName.AsSpan(), stringBufferSpan[numberOfBytesWritten..]);
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

                // sagaEntityTypeFullName_correlationPropertyName_serializedPropertyValue
                numberOfBytesWritten += encoding.GetBytes(serializedPropertyValue.AsSpan(), stringBufferSpan[numberOfBytesWritten..]);

                Span<byte> hashBuffer = stackalloc byte[20];
                _ = SHA1.HashData(stringBufferSpan[..numberOfBytesWritten], hashBuffer);
                var guidBytes = hashBuffer[..16];
                return new Guid(guidBytes);
            }
            finally
            {
                if (sharedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(sharedBuffer, clearArray: true);
                }
            }
        }
    }
}
