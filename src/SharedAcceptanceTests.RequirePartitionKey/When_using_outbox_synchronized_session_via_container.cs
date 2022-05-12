﻿namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    public partial class When_using_outbox_synchronized_session_via_container : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_inject_synchronized_session_into_handler()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(s => s.SendLocal(new MyMessage())))
                .Done(c => c.Done)
                .Run()
                .ConfigureAwait(false);

            Assert.True(context.RepositoryHasBatch);
            Assert.True(context.RepositoryHasContainer);
            AssertPartitionPart(context);
        }

        partial void AssertPartitionPart(Context scenarioContext);

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool RepositoryHasBatch { get; set; }
            public bool RepositoryHasContainer { get; set; }
            public PartitionKey PartitionKey { get; set; }
            public PartitionKeyPath PartitionKeyPath { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.EnableOutbox();
                    config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                    config.RegisterComponents(c =>
                    {
                        c.AddScoped<MyRepository>();
                    });
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(MyRepository repository, Context context)
                {
                    this.context = context;
                    this.repository = repository;
                }


                public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
                {
                    repository.DoSomething();
                    context.Done = true;
                    return Task.CompletedTask;
                }

                Context context;
                MyRepository repository;
            }
        }

        public class MyRepository
        {
            public MyRepository(ICosmosStorageSession storageSession, Context context)
            {
                this.storageSession = storageSession;
                this.context = context;
            }

            public void DoSomething()
            {
                context.RepositoryHasBatch = storageSession.Batch != null;
                context.RepositoryHasContainer = storageSession.Container != null;
                context.PartitionKey = storageSession.PartitionKey;
                context.PartitionKeyPath = storageSession.PartitionKeyPath;
            }

            ICosmosStorageSession storageSession;
            Context context;
        }

        public class MyMessage : IMessage
        {
            public string Property { get; set; }
        }
    }
}