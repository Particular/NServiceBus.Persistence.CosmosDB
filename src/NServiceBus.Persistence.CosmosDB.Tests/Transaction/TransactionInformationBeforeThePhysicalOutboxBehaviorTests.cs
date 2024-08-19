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
        public async Task Should_not_set_partition_key_when_partition_key_extractor_returns_false()
        {
            var extractor = new PartitionKeyExtractor(
                (IReadOnlyDictionary<string, string> headers, out PartitionKey? key) =>
                {
                    key = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(extractor, new ContainerInformationExtractor());

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out _), Is.False);
        }

        [Test]
        public async Task Should_set_partition_key_when_partition_key_extractor_returns_true()
        {
            var partitionKeyExtractor = new PartitionKeyExtractor(
                (IReadOnlyDictionary<string, string> headers, out PartitionKey? key) =>
                {
                    key = new PartitionKey(true);
                    return true;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(partitionKeyExtractor, new ContainerInformationExtractor());

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out var partitionKey), Is.True);
            Assert.That(partitionKey, Is.EqualTo(new PartitionKey(true)));
        }

        [Test]
        public async Task Should_not_set_container_when_container_information_extractor_returns_false()
        {
            var containerInformationExtractor = new ContainerInformationExtractor(
                (IReadOnlyDictionary<string, string> headers, out ContainerInformation? container) =>
                {
                    container = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new PartitionKeyExtractor(), containerInformationExtractor);

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out _), Is.False);
        }

        [Test]
        public async Task Should_set_container_when_container_information_extractor_returns_true()
        {
            var containerInformationExtractor = new ContainerInformationExtractor(
                (IReadOnlyDictionary<string, string> headers, out ContainerInformation? container) =>
                {
                    container = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));
                    return true;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new CosmosDB.PartitionKeyExtractor(), containerInformationExtractor);

            var context = new TestableTransportReceiveContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out var containerInformation), Is.True);
            Assert.That(containerInformation, Is.EqualTo(new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"))));
        }

        [Test]
        public async Task Should_pass_headers_to_partition_key_extractor()
        {
            IReadOnlyDictionary<string, string> capturedHeaders = null;
            var partitionKeyExtractor = new PartitionKeyExtractor(
                (IReadOnlyDictionary<string, string> headers, out PartitionKey? key) =>
                {
                    key = null;
                    capturedHeaders = headers;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(partitionKeyExtractor, new ContainerInformationExtractor());

            var context = new TestableTransportReceiveContext();
            context.Message.Headers.Add("TheAnswer", "Is42");

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(capturedHeaders, Is.EqualTo(context.Message.Headers));
        }

        [Test]
        public async Task Should_pass_headers_to_container_information_extractor()
        {
            IReadOnlyDictionary<string, string> capturedHeaders = null;
            var containerInformationExtractor = new ContainerInformationExtractor(
                (IReadOnlyDictionary<string, string> headers, out ContainerInformation? container) =>
                {
                    container = null;
                    capturedHeaders = headers;
                    return false;
                });

            var behavior = new TransactionInformationBeforeThePhysicalOutboxBehavior(new PartitionKeyExtractor(), containerInformationExtractor);

            var context = new TestableTransportReceiveContext();
            context.Message.Headers.Add("TheAnswer", "Is42");

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(capturedHeaders, Is.EqualTo(context.Message.Headers));
        }

        delegate bool TryExtractPartitionKey(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey);

        class PartitionKeyExtractor : IPartitionKeyFromHeadersExtractor
        {
            readonly TryExtractPartitionKey tryExtract;

            public PartitionKeyExtractor(TryExtractPartitionKey tryExtract = default)
            {
                if (tryExtract == null)
                {
                    this.tryExtract = (IReadOnlyDictionary<string, string> headers,
                        out PartitionKey? partitionKey) =>
                    {
                        partitionKey = null;
                        return false;
                    };
                    return;
                }

                this.tryExtract = tryExtract;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey) =>
                tryExtract(headers, out partitionKey);
        }

        delegate bool TryExtractContainerInformation(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation);

        class ContainerInformationExtractor : IContainerInformationFromHeadersExtractor
        {
            readonly TryExtractContainerInformation tryExtract;

            public ContainerInformationExtractor(TryExtractContainerInformation tryExtract = default)
            {
                if (tryExtract == null)
                {
                    this.tryExtract = (IReadOnlyDictionary<string, string> headers,
                        out ContainerInformation? containerInformation) =>
                    {
                        containerInformation = null;
                        return false;
                    };
                    return;
                }

                this.tryExtract = tryExtract;
            }

            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation) =>
                tryExtract(headers, out containerInformation);
        }
    }
}