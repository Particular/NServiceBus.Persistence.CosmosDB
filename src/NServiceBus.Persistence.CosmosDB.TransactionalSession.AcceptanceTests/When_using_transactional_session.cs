namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using global::NServiceBus.AcceptanceTests;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class When_using_transactional_session : NServiceBusAcceptanceTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_send_messages_and_store_document_in_synchronized_session_on_transactional_session_commit(bool outboxEnabled)
        {
            var documentId = Guid.NewGuid().ToString();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString()), SetupFixture.DefaultContainerInformation));

                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();

                    await transactionalSession.SendLocal(new SampleMessage());

                    var storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                    storageSession.Batch.CreateItem(new MyDocument
                    {
                        Id = documentId,
                        Data = "SomeData",
                        deep = new Deep { down = ctx.TestRunId.ToString() }
                    });

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            var response = await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString()));

            Assert.IsNotNull(response);
            Assert.AreEqual("SomeData", response.Resource.Data);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_send_messages_and_store_document_in_cosmos_session_on_transactional_session_commit(bool outboxEnabled)
        {
            var documentId = Guid.NewGuid().ToString();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString()), SetupFixture.DefaultContainerInformation));

                    await transactionalSession.SendLocal(new SampleMessage());

                    var storageSession = scope.ServiceProvider.GetRequiredService<ICosmosStorageSession>();
                    storageSession.Batch.CreateItem(new MyDocument
                    {
                        Id = documentId,
                        Data = "SomeData",
                        deep = new Deep { down = ctx.TestRunId.ToString() }
                    });

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            var response = await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString()));

            Assert.IsNotNull(response);
            Assert.AreEqual("SomeData", response.Resource.Data);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_not_send_messages_if_session_is_not_committed(bool outboxEnabled)
        {
            var documentId = Guid.NewGuid().ToString();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
                {
                    using (var scope = ctx.ServiceProvider.CreateScope())
                    using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                    {
                        await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString()), SetupFixture.DefaultContainerInformation));

                        await transactionalSession.SendLocal(new SampleMessage());

                        var storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                        storageSession.Batch.CreateItem(new MyDocument
                        {
                            Id = documentId,
                            Data = "SomeData",
                            deep = new Deep { down = ctx.TestRunId.ToString() }
                        });
                    }

                    //Send immediately dispatched message to finish the test
                    await statelessSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();


            Assert.True(context.CompleteMessageReceived);
            Assert.False(context.MessageReceived);

            var exception = Assert.ThrowsAsync<CosmosException>(async () =>
                await SetupFixture.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(context.TestRunId.ToString())));
            Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed(bool outboxEnabled)
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(new CosmosOpenSessionOptions(new PartitionKey(ctx.TestRunId.ToString()), SetupFixture.DefaultContainerInformation));

                    var sendOptions = new SendOptions();
                    sendOptions.RequireImmediateDispatch();
                    sendOptions.RouteToThisEndpoint();
                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);
                }))
                .Done(c => c.MessageReceived)
                .Run()
                ;

            Assert.True(result.MessageReceived);
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

            class SampleHandler : IHandleMessages<SampleMessage>
            {
                public SampleHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
            {
                public CompleteTestMessageHandler(Context context) => testContext = context;

                public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
                {
                    testContext.CompleteMessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
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
            [JsonProperty("id")]
            public string Id { get; set; }
            public string Data { get; set; }

#pragma warning disable IDE1006
            public Deep deep { get; set; }
#pragma warning restore IDE1006
        }

        class Deep
        {
#pragma warning disable IDE1006
            public string down { get; set; }
#pragma warning restore IDE1006
        }
    }
}