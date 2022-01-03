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

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(extractor);

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out _), Is.False);
            Assert.That(context.Extensions.TryGet<ContainerInformation>(out _), Is.False);
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

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(extractor);

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

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(extractor);

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out var containerInformation), Is.True);
            Assert.AreEqual(new ContainerInformation("containerName", new PartitionKeyPath("/deep/down")), containerInformation);
        }

        delegate bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
            out ContainerInformation? containerInformation);

        class Extractor : IExtractTransactionInformationFromHeaders
        {
            readonly TryExtract tryExtract;

            public Extractor(TryExtract tryExtract) => this.tryExtract = tryExtract;

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
                out ContainerInformation? containerInformation) =>
                tryExtract(headers, out partitionKey, out containerInformation);
        }
    }
}