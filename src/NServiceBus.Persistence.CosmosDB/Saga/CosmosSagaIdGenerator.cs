namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using Newtonsoft.Json;

    static class CosmosSagaIdGenerator
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
            var byteCount = Encoding.UTF8.GetByteCount(src);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var numberOfBytes = Encoding.UTF8.GetBytes(src.AsSpan(), buffer);

                using (var sha1CryptoServiceProvider = SHA1.Create())
                {
                    var guidBytes = sha1CryptoServiceProvider.ComputeHash(buffer, 0, numberOfBytes).AsSpan().Slice(0, 16);
                    if (!MemoryMarshal.TryRead<Guid>(guidBytes, out var deterministicGuid))
                    {
                        deterministicGuid = new Guid(guidBytes.ToArray());
                    }
                    return deterministicGuid;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
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
