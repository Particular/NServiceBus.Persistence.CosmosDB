namespace NServiceBus.PersistenceTesting.Outbox;

using System;
using System.Threading.Tasks;
using Extensibility;
using NServiceBus.Outbox;
using NUnit.Framework;

[TestFixtureSource(typeof(PersistenceTestsConfiguration), "OutboxVariants")]
class ExtendedOutboxStorageTests(TestVariant param)
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        configuration = new PersistenceTestsConfiguration(param) { OutboxTimeToLiveInSeconds = 1 };
        await configuration.Configure();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await configuration.Cleanup();

    [Test]
    public async Task Should_expire_dispatched_messages()
    {
        configuration.RequiresOutboxSupport();

        IOutboxStorage storage = configuration.OutboxStorage;
        ContextBag ctx = configuration.GetContextBagForOutbox();

        string messageId = Guid.NewGuid().ToString();
        await storage.Get(messageId, ctx);

        var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
        using (IOutboxTransaction transaction = await storage.BeginTransaction(ctx))
        {
            await storage.Store(messageToStore, transaction, ctx);

            await transaction.Commit();
        }

        await storage.SetAsDispatched(messageId, configuration.GetContextBagForOutbox());

        OutboxMessage message = null;
        for (int i = 1; i < 10; i++)
        {
            message = await storage.Get(messageId, configuration.GetContextBagForOutbox());
            if (message != null)
            {
                await Task.Delay(i * 550);
            }
        }

        Assert.That(message, Is.Null, "The outbox record was not expired.");
    }

    IPersistenceTestsConfiguration configuration;
}