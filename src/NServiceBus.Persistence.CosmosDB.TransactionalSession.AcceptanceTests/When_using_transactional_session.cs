namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using NUnit.Framework;

public class When_using_transactional_session : NServiceBusAcceptanceTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_send_messages_and_store_document_in_synchronized_session_on_transactional_session_commit(bool outboxEnabled)
    {
        string documentId = Guid.NewGuid().ToString();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using IServiceScope scope = ctx.ServiceProvider.CreateScope();
                using ITransactionalSession transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                await transactionalSession.SendLocal(new SampleMessage());

                ICosmosStorageSession storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                storageSession.Batch.CreateItem(new MyDocument
                {
                    Id = documentId,
                    Data = "SomeData",
                    PartitionKey = ctx.TestRunId.ToString()
                });

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        ItemResponse<MyDocument> response = await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString()));

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Resource.Data, Is.EqualTo("SomeData"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_send_messages_and_store_document_in_cosmos_session_on_transactional_session_commit(bool outboxEnabled)
    {
        string documentId = Guid.NewGuid().ToString();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using IServiceScope scope = ctx.ServiceProvider.CreateScope();
                using ITransactionalSession transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                await transactionalSession.SendLocal(new SampleMessage());

                ICosmosStorageSession storageSession = scope.ServiceProvider.GetRequiredService<ICosmosStorageSession>();
                storageSession.Batch.CreateItem(new MyDocument
                {
                    Id = documentId,
                    Data = "SomeData",
                    PartitionKey = ctx.TestRunId.ToString()
                });

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        ItemResponse<MyDocument> response = await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString()));

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Resource.Data, Is.EqualTo("SomeData"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_not_send_messages_if_session_is_not_committed(bool outboxEnabled)
    {
        string documentId = Guid.NewGuid().ToString();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
            {
                using (IServiceScope scope = ctx.ServiceProvider.CreateScope())
                using (ITransactionalSession transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                {
                    await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                    await transactionalSession.SendLocal(new SampleMessage());

                    ICosmosStorageSession storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                    storageSession.Batch.CreateItem(new MyDocument
                    {
                        Id = documentId,
                        Data = "SomeData",
                        PartitionKey = ctx.TestRunId.ToString()
                    });
                }

                //Send immediately dispatched message to finish the test
                await statelessSession.SendLocal(new CompleteTestMessage());
            }))
            .Done(c => c.CompleteMessageReceived)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.CompleteMessageReceived, Is.True);
            Assert.That(context.MessageReceived, Is.False);
        });

        CosmosException exception = Assert.ThrowsAsync<CosmosException>(async () =>
            await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString())));
        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed(bool outboxEnabled)
    {
        Context result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using IServiceScope scope = ctx.ServiceProvider.CreateScope();
                using ITransactionalSession transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                var sendOptions = new SendOptions();
                sendOptions.RequireImmediateDispatch();
                sendOptions.RouteToThisEndpoint();
                await transactionalSession.Send(new SampleMessage(), sendOptions);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(result.MessageReceived, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_allow_using_synchronized_storage_even_when_there_are_no_outgoing_operations(bool outboxEnabled)
    {
        string documentId = Guid.NewGuid().ToString();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
            {
                using (IServiceScope scope = ctx.ServiceProvider.CreateScope())
                using (ITransactionalSession transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                {
                    await transactionalSession.Open(
                        new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString())));

                    ICosmosStorageSession storageSession = scope.ServiceProvider.GetRequiredService<ICosmosStorageSession>();
                    storageSession.Batch.CreateItem(new MyDocument
                    {
                        Id = documentId,
                        Data = "SomeData",
                        PartitionKey = ctx.TestRunId.ToString()
                    });

                    // Deliberately not sending any messages via the transactional session before committing
                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }

                //Send immediately dispatched message to finish the test
                await statelessSession.SendLocal(new CompleteTestMessage());
            }))
            .Done(c => c.CompleteMessageReceived)
            .Run();

        ItemResponse<MyDocument> response = await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString()));

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Resource.Data, Is.EqualTo("SomeData"));
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public bool MessageReceived { get; set; }
        public bool CompleteMessageReceived { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint()
        {
            if ((bool)TestContext.CurrentContext.Test.Arguments[0]!)
            {
                EndpointSetup<TransactionSessionDefaultServer>();
            }
            else
            {
                EndpointSetup<TransactionSessionWithOutboxEndpoint>();
            }
        }

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    class SampleMessage : ICommand
    {
    }

    class CompleteTestMessage : ICommand
    {
    }

    class MyDocument
    {
        [JsonProperty("id")] public string Id { get; set; }
        public string Data { get; init; }

        [JsonProperty(SetupFixture.PartitionPropertyName)]
        public string PartitionKey { get; set; }
    }
}