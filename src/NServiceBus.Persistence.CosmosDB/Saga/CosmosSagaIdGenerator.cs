namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Buffers;
#if NETFRAMEWORK
    using System.Runtime.InteropServices;
#endif
#if NET
    using System.Runtime.CompilerServices;
#endif
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

#if NETFRAMEWORK
        static Guid DeterministicGuid(string sagaEntityTypeFullName, string correlationPropertyName, string serializedPropertyValue)
        {
            // sagaEntityTypeFullName_correlationPropertyName_serializedPropertyValue
            var length = sagaEntityTypeFullName.Length + correlationPropertyName.Length + serializedPropertyValue.Length + 2;

            var encoding = Encoding.UTF8;
            var maxByteCount = encoding.GetMaxByteCount(length);
            var stringBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
            var stringBufferSpan = stringBuffer.AsSpan();

            try
            {
                var numberOfBytesWritten = encoding.GetBytes(sagaEntityTypeFullName.AsSpan(), stringBufferSpan);
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

                numberOfBytesWritten += encoding.GetBytes(correlationPropertyName.AsSpan(), stringBufferSpan.Slice(numberOfBytesWritten));
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

                numberOfBytesWritten += encoding.GetBytes(serializedPropertyValue.AsSpan(), stringBufferSpan.Slice(numberOfBytesWritten));

                using var sha1CryptoServiceProvider = SHA1.Create();
                var guidBytes = sha1CryptoServiceProvider.ComputeHash(stringBuffer, 0, numberOfBytesWritten).AsSpan().Slice(0, 16);
                if (!MemoryMarshal.TryRead<Guid>(guidBytes, out var deterministicGuid))
                {
                    deterministicGuid = new Guid(guidBytes.ToArray());
                }
                return deterministicGuid;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(stringBuffer, clearArray: true);
            }
        }
#endif
#if NET
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
                var numberOfBytesWritten = encoding.GetBytes(sagaEntityTypeFullName.AsSpan(), stringBufferSpan);
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

                numberOfBytesWritten += encoding.GetBytes(correlationPropertyName.AsSpan(), stringBufferSpan[numberOfBytesWritten..]);
                stringBufferSpan[numberOfBytesWritten++] = SeparatorByte;

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
#endif
    }

#if NETFRAMEWORK
    static class SpanExtensions
    {
        public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> src, Span<byte> dst)
        {
            if (src.IsEmpty)
            {
                return 0;
            }

            fixed (char* chars = &src.GetPinnableReference())
            fixed (byte* bytes = &dst.GetPinnableReference())
            {
                return encoding.GetBytes(chars, src.Length, bytes, dst.Length);
            }
        }
    }
#endif
}
