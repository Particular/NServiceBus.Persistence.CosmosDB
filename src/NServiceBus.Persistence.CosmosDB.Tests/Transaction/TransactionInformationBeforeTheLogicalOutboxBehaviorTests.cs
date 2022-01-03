namespace NServiceBus.Persistence.CosmosDB.Tests.Transaction
{
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
        public async Task Should_not_set_partition_key_or_container_when_extractor_returns_false()
        {
            var extractor = new Extractor(
                delegate (object msg, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    return false;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(extractor);

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out _), Is.False);
            Assert.That(context.Extensions.TryGet<ContainerInformation>(out _), Is.False);
        }

        [Test]
        public async Task Should_set_partition_key_when_extractor_returns_true()
        {
            var extractor = new Extractor(
                delegate (object msg, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = new PartitionKey(true);
                    container = null;
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(extractor);

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<PartitionKey>(out var partitionKey), Is.True);
            Assert.AreEqual(new PartitionKey(true), partitionKey);
        }

        [Test]
        public async Task Should_set_container_when_extractor_returns_true()
        {
            var extractor = new Extractor(
                delegate (object msg, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = new ContainerInformation("containerName", new PartitionKeyPath("/deep/down"));
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(extractor);

            var context = new TestableIncomingLogicalMessageContext();

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(context.Extensions.TryGet<ContainerInformation>(out var containerInformation), Is.True);
            Assert.AreEqual(new ContainerInformation("containerName", new PartitionKeyPath("/deep/down")), containerInformation);
        }

        [Test]
        public async Task Should_pass_message()
        {
            object capturedMessageInstance = null;
            var extractor = new Extractor(
                delegate (object msg, out PartitionKey? key, out ContainerInformation? container)
                {
                    key = null;
                    container = null;
                    capturedMessageInstance = msg;
                    return true;
                });

            var behavior = new TransactionInformationBeforeTheLogicalOutboxBehavior(extractor);

            var context = new TestableIncomingLogicalMessageContext();
            var messageInstance = new object();
            context.Message = new LogicalMessage(new MessageMetadata(typeof(object)), messageInstance);

            await behavior.Invoke(context, _ => Task.CompletedTask);

            Assert.That(capturedMessageInstance, Is.Not.Null.And.EqualTo(messageInstance));
        }

        delegate bool TryExtract(object message, out PartitionKey? partitionKey,
            out ContainerInformation? containerInformation);

        class Extractor : IExtractTransactionInformationFromMessages
        {
            readonly TryExtract tryExtract;

            public Extractor(TryExtract tryExtract) => this.tryExtract = tryExtract;

            public bool TryExtract(object message, out PartitionKey? partitionKey,
                out ContainerInformation? containerInformation) =>
                tryExtract(message, out partitionKey, out containerInformation);
        }
    }
}