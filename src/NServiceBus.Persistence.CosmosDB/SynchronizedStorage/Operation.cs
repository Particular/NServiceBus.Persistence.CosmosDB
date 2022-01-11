namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Net;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    interface IReleaseLockOperation : IOperation
    {
    }

    interface IOperation : IDisposable
    {
        PartitionKey PartitionKey { get; }
        void Success(TransactionalBatchOperationResult result);
        void Conflict(TransactionalBatchOperationResult result);
        void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath);
    }

    abstract class Operation : IOperation
    {
        protected Operation(PartitionKey partitionKey, JsonSerializer serializer, ContextBag context)
        {
            PartitionKey = partitionKey;
            Serializer = serializer;
            Context = context;
        }

        public ContextBag Context { get; }
        public PartitionKey PartitionKey { get; }
        public JsonSerializer Serializer { get; }

        public virtual void Success(TransactionalBatchOperationResult result)
        {
        }

        public virtual void Conflict(TransactionalBatchOperationResult result)
        {
            if ((int)result.StatusCode == 424) // HttpStatusCode.FailedDependency:
            {
                return;
            }

            switch (result.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    throw new TransactionalBatchOperationException("Bad request. Likely the partition key did not match.", result);
                case HttpStatusCode.Conflict:
                case HttpStatusCode.PreconditionFailed:
                    throw new TransactionalBatchOperationException("Concurrency conflict.", result);
                case HttpStatusCode.Accepted:
                case HttpStatusCode.Ambiguous:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.Continue:
                case HttpStatusCode.Created:
                case HttpStatusCode.ExpectationFailed:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Found:
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.Gone:
                case HttpStatusCode.HttpVersionNotSupported:
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.LengthRequired:
                case HttpStatusCode.MethodNotAllowed:
                case HttpStatusCode.Moved:
                case HttpStatusCode.NoContent:
                case HttpStatusCode.NonAuthoritativeInformation:
                case HttpStatusCode.NotAcceptable:
                case HttpStatusCode.NotFound:
                case HttpStatusCode.NotImplemented:
                case HttpStatusCode.NotModified:
                case HttpStatusCode.OK:
                case HttpStatusCode.PartialContent:
                case HttpStatusCode.PaymentRequired:
                case HttpStatusCode.ProxyAuthenticationRequired:
                case HttpStatusCode.RedirectKeepVerb:
                case HttpStatusCode.RedirectMethod:
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                case HttpStatusCode.RequestEntityTooLarge:
                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.RequestUriTooLong:
                case HttpStatusCode.ResetContent:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.SwitchingProtocols:
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.UnsupportedMediaType:
                case HttpStatusCode.Unused:
                case HttpStatusCode.UpgradeRequired:
                case HttpStatusCode.UseProxy:
                default:
                    throw new TransactionalBatchOperationException(result);
            }
        }
        public abstract void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath);
        public virtual void Dispose() { }

        protected void EnrichWithPartitionKeyIfNecessary(JObject toBeEnriched, PartitionKeyPath partitionKeyPath)
        {
            var partitionKeyAsJArray = JArray.Parse(PartitionKey.ToString())[0];
            // we should probably optimize this a bit and the result might be cacheable but let's worry later
            var pathToMatch = partitionKeyPath.ToString().Replace("/", ".");
            var segments = pathToMatch.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);

            var start = new JObject();
            var current = start;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = segments[i];

                if (i == segments.Length - 1)
                {
                    current[segmentName] = partitionKeyAsJArray;
                    continue;
                }

                current[segmentName] = new JObject();
                current = (JObject)current[segmentName];
            }

            // promote it if not there, what if the user has it and the key doesn't match?
            var matchToken = toBeEnriched.SelectToken(pathToMatch);
            if (matchToken == null)
            {
                toBeEnriched.Merge(start);
            }
        }
    }

    class ThrowOnConflictOperation : Operation
    {
        ThrowOnConflictOperation() : base(PartitionKey.Null, null, null)
        {
        }

        public static IOperation Instance { get; } = new ThrowOnConflictOperation();

        public override void Apply(TransactionalBatch transactionalBatch, PartitionKeyPath partitionKeyPath)
        {
        }
    }
}