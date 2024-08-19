namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Pipeline;
    using Testing;
    using Unicast.Messages;

    [TestFixture]
    public class TransactionInformationBeforeTheLogicalOutboxBehaviorTests
    {
        [Test]
        public async Task Should_not_set_partition_key_when_partition_key_extractor_returns_false()
        {
            var extractor = new PartitionKeyExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers, out PartitionKey? key) =>
                {
                    key = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(extractor, new ContainerInformationExtractor());

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out _), Is.False);
        }

        [Test]
        public async Task Should_set_partition_key_when_partition_key_extractor_returns_true()
        {
            var partitionKeyExtractor = new PartitionKeyExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers, out PartitionKey? key) =>
                {
                    key = new PartitionKey(true);
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(partitionKeyExtractor, new ContainerInformationExtractor());

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.Multiple(() =>
            {
                Assert.That(context.Extensions.TryGet<PartitionKey>(out var partitionKey), Is.True);
                Assert.That(partitionKey, Is.EqualTo(new PartitionKey(true)));
            });
        }

        [Test]
        public async Task Should_not_set_container_information_when_container_information_extractor_returns_false()
        {
            var extractor = new ContainerInformationExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers, out ContainerInformation? container) =>
                {
                    container = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(new CosmosDB.PartitionKeyExtractor(), extractor);

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out _), Is.False);
        }

        [Test]
        public async Task Should_set_container_when_container_information_extractor_returns_true()
        {
            var extractor = new ContainerInformationExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers, out ContainerInformation? container) =>
                {
                    container = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(new PartitionKeyExtractor(), extractor);

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.Multiple(() =>
            {
                Assert.That(context.Extensions.TryGet<ContainerInformation>(out var containerInformation), Is.True);
                Assert.That(containerInformation, Is.EqualTo(new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"))));
            });
        }

        [Test]
        public async Task Should_pass_message_and_headers_to_partition_key_extractor()
        {
            object capturedMessageInstanceFromPartionKeyExtractor = null;
            IReadOnlyDictionary<string, string> capturedHeadersFromPartionKeyExtractor = null;
            var partitionKeyExtractor = new PartitionKeyExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey) =>
                {
                    partitionKey = null;
                    capturedMessageInstanceFromPartionKeyExtractor = msg;
                    capturedHeadersFromPartionKeyExtractor = headers;
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(partitionKeyExtractor, new ContainerInformationExtractor());

            var messageHeaders = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };
            var messageInstance = new object();
            var context = new TestableIncomingLogicalMessageContext
            {
                MessageHeaders = messageHeaders,
                Message = new LogicalMessage(new MessageMetadata(typeof(object)), messageInstance)
            };

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.Multiple(() =>
            {
                Assert.That(capturedMessageInstanceFromPartionKeyExtractor, Is.Not.Null.And.EqualTo(messageInstance));
                Assert.That(capturedHeadersFromPartionKeyExtractor, Is.Not.Null.And.EqualTo(messageHeaders));
            });
        }

        [Test]
        public async Task Should_pass_message_and_headers_to_container_information_extractor()
        {
            object capturedMessageInstanceFromContainerInformationExtractor = null;
            IReadOnlyDictionary<string, string> capturedHeadersFromContainerInformationExtractor = null;
            var containerInformationExtractor = new ContainerInformationExtractor(
                (object msg, IReadOnlyDictionary<string, string> headers,
                    out ContainerInformation? containerInformation) =>
                {
                    containerInformation = null;
                    capturedMessageInstanceFromContainerInformationExtractor = msg;
                    capturedHeadersFromContainerInformationExtractor = headers;
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(new PartitionKeyExtractor(), containerInformationExtractor);

            var messageHeaders = new Dictionary<string, string> { { "HeaderKey", "HeaderValue" } };
            var messageInstance = new object();
            var context = new TestableIncomingLogicalMessageContext
            {
                MessageHeaders = messageHeaders,
                Message = new LogicalMessage(new MessageMetadata(typeof(object)), messageInstance)
            };

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.Multiple(() =>
            {
                Assert.That(capturedMessageInstanceFromContainerInformationExtractor, Is.Not.Null.And.EqualTo(messageInstance));
                Assert.That(capturedHeadersFromContainerInformationExtractor, Is.Not.Null.And.EqualTo(messageHeaders));
            });
        }

        delegate bool TryExtractPartitionKey(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey);

        class PartitionKeyExtractor : IPartitionKeyFromMessageExtractor
        {
            readonly TryExtractPartitionKey tryExtract;

            public PartitionKeyExtractor(TryExtractPartitionKey tryExtract = default)
            {
                if (tryExtract == null)
                {
                    this.tryExtract = (object msg, IReadOnlyDictionary<string, string> headers,
                        out PartitionKey? partitionKey) =>
                    {
                        partitionKey = null;
                        return false;
                    };
                    return;
                }

                this.tryExtract = tryExtract;
            }

            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey) =>
                tryExtract(message, headers, out partitionKey);
        }

        delegate bool TryExtractContainerInformation(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation);

        class ContainerInformationExtractor : IContainerInformationFromMessagesExtractor
        {
            readonly TryExtractContainerInformation tryExtract;

            public ContainerInformationExtractor(TryExtractContainerInformation tryExtract = default)
            {
                if (tryExtract == null)
                {
                    this.tryExtract = (object msg, IReadOnlyDictionary<string, string> headers,
                        out ContainerInformation? containerInformation) =>
                    {
                        containerInformation = null;
                        return false;
                    };
                    return;
                }

                this.tryExtract = tryExtract;
            }

            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation) =>
                tryExtract(message, headers, out containerInformation);
        }
    }
}