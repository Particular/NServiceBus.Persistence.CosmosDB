namespace NServiceBus.PersistenceTesting.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Outbox;
    using NUnit.Framework;
    using Sagas;

    [TestFixtureSource(typeof(PersistenceTestsConfiguration), "OutboxVariants")]
    class ExtendedOutboxStorageTests
    {
        public ExtendedOutboxStorageTests(TestVariant param)
        {
            this.param = param;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            configuration = new PersistenceTestsConfiguration(param) { OutboxTimeToLiveInSeconds = 1 };
            await configuration.Configure();
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await configuration.Cleanup();
        }

        [Test]
        public async Task Should_expire_dispatched_messages()
        {
            configuration.RequiresOutboxSupport();

            var storage = configuration.OutboxStorage;
            var ctx = configuration.GetContextBagForOutbox();

            var messageId = Guid.NewGuid().ToString();
            await storage.Get(messageId, ctx);

            var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
            using (var transaction = await storage.BeginTransaction(ctx))
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

        //TODO: should be removed during clean-up after pessimistic locking implementation
        [Test]
        public async Task TestPatchTest()
        {
            var gd = Guid.NewGuid().ToString();

            await SetupFixture.CosmosDbClient.GetDatabase(SetupFixture.DatabaseName).CreateContainerIfNotExistsAsync(gd, "/id");
            var container = SetupFixture.CosmosDbClient.GetContainer(SetupFixture.DatabaseName, gd);

            var pK = new PartitionKey(gd);
            long unixTimeNow = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            //await container.CreateItemAsync(new TestPatch { ReserveUntil = unixTimeNow + 1, id = gd }, pK);
            await container.CreateItemAsync(new TestPatch { id = gd }, pK);

            var piro = new PatchItemRequestOptions
            {
                FilterPredicate = $"from c where (NOT IS_DEFINED(c.ReserveUntil) OR c.ReserveUntil < {unixTimeNow})"
            };

            try
            {
                var patchItemAsyncRes = await container.PatchItemAsync<TestPatch>(gd, pK, new List<PatchOperation> { PatchOperation.Add("/ReserveUntil", unixTimeNow + 6) }, piro);

                Console.WriteLine(patchItemAsyncRes);
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine(ce);
                throw;
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                Console.WriteLine(ce);
                throw;
            }

        }

        public class TestPatch
        {
#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
            public long? ReserveUntil { get; set; }
        }

        IPersistenceTestsConfiguration configuration;
        TestVariant param;
    }
}