namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class When_using_synchronized_session_via_container : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_inject_synchronized_session_into_handler()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(s => s.SendLocal(new MyMessage())))
                .Done(c => c.Done)
                .Run()
                .ConfigureAwait(false);

            Assert.IsNotNull(context.BatchInjectedToFirstHandler);
            Assert.IsNotNull(context.BatchInjectedToSecondHandler);
            Assert.IsNotNull(context.BatchInjectedToThirdHandler);
            Assert.AreSame(context.BatchInjectedToFirstHandler, context.BatchInjectedToSecondHandler);
            Assert.AreNotSame(context.BatchInjectedToFirstHandler, context.BatchInjectedToThirdHandler);
        }

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public TransactionalBatch BatchInjectedToFirstHandler { get; set; }
            public TransactionalBatch BatchInjectedToSecondHandler { get; set; }
            public TransactionalBatch BatchInjectedToThirdHandler { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(ICosmosDBStorageSession session, Context context)
                {
                    this.session = session;
                    this.context = context;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
                {
                    context.BatchInjectedToFirstHandler = session.Batch;
                    return handlerContext.SendLocal(new MyFollowUpMessage
                    {
                        Property = message.Property
                    });
                }

                Context context;
                ICosmosDBStorageSession session;
            }

            public class MyOtherMessageHandler : IHandleMessages<MyMessage>
            {
                public MyOtherMessageHandler(ICosmosDBStorageSession session, Context context)
                {
                    this.session = session;
                    this.context = context;
                }


                public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
                {
                    context.BatchInjectedToSecondHandler = session.Batch;
                    return Task.CompletedTask;
                }

                Context context;
                ICosmosDBStorageSession session;
            }

            public class MyFollowUpMessageHandler : IHandleMessages<MyFollowUpMessage>
            {
                public MyFollowUpMessageHandler(ICosmosDBStorageSession session, Context context)
                {
                    this.session = session;
                    this.context = context;
                }


                public Task Handle(MyFollowUpMessage message, IMessageHandlerContext handlerContext)
                {
                    context.BatchInjectedToThirdHandler = session.Batch;
                    context.Done = true;
                    return Task.CompletedTask;
                }

                Context context;
                ICosmosDBStorageSession session;
            }
        }

        public class MyMessage : IMessage
        {
            public string Property { get; set; }
        }

        public class MyFollowUpMessage : IMessage
        {
            public string Property { get; set; }
        }
    }
}