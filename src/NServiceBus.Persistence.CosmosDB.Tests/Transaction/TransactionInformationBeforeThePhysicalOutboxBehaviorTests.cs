namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class TransactionInformationBeforeThePhysicalOutboxBehaviorTests
    {
        [Test]
        public async Task Should_not_set_partition_key_or_container_when_extractor_returns_false()
        {
            var extractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new[] { extractor });

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out _), Is.False);
            Assert.That(context.Extensions.TryGet<ContainerInformation>(out _), Is.False);
        }

        [Test]
        public async Task Should_skip_remaining_extractors_once_one_returns_true()
        {
            bool lastWasCalled = false;
            var firstExtractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    return false;
                });

            var matchingExtractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    return true;
                });

            var lastExtractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    lastWasCalled = true;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new[] { firstExtractor, matchingExtractor, lastExtractor });

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(lastWasCalled, Is.False);
        }

        [Test]
        public async Task Should_set_partition_key_when_extractor_returns_true()
        {
            var extractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = new PartitionKey(true);
                    container = null;
                    return true;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new[] { extractor });

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out var partitionKey), Is.True);
            Assert.AreEqual(new PartitionKey(true), partitionKey);
        }

        [Test]
        public async Task Should_set_container_when_extractor_returns_true()
        {
            var extractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));
                    return true;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new[] { extractor });

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out var containerInformation), Is.True);
            Assert.AreEqual(new ContainerInformation("containerName", new PartitionKeyPath("/deep/down")), containerInformation);
        }

        [Test]
        public async Task Should_pass_headers()
        {
            IReadOnlyDictionary<string, string> capturedHeaders = null;
            var extractor = new Extractor(
                delegate (IReadOnlyDictionary<string, string> headers, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    capturedHeaders = headers;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new[] { extractor });

            var context = new TestableTransportReceiveContext();
            context.Message.Headers.Clear();
            context.Message.Headers.Add("TheAnswer", "Is42");

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(capturedHeaders, Is.Not.Null.And.ContainKey("TheAnswer").WithValue("Is42").And.Count.EqualTo(1));
        }

        delegate bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
            out ContainerInformation? containerInformation);

        class Extractor : ITransactionInformationFromHeadersExtractor
        {
            readonly TryExtract tryExtract;

            public Extractor(TryExtract tryExtract) => this.tryExtract = tryExtract;

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
                out ContainerInformation? containerInformation) =>
                tryExtract(headers, out partitionKey, out containerInformation);
        }
    }
}