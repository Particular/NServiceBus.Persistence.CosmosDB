namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    [TestFixture]
    public class When_using_outbox_synchronized_session_via_container : NServiceBusAcceptanceTest
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
        }

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool RepositoryHasBatch { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.EnableOutbox();
                    config.RegisterComponents(c =>
                    {
                        c.ConfigureComponent<MyRepository>(DependencyLifecycle.InstancePerUnitOfWork);
                        c.ConfigureComponent(b =>
                        {
                            var session = b.Build<ICosmosDBStorageSession>();
                            return session?.Batch;
                        }, DependencyLifecycle.InstancePerUnitOfWork);
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
            public MyRepository(TransactionalBatch batch, Context context)
            {
                this.batch = batch;
                this.context = context;
            }

            public void DoSomething()
            {
                context.RepositoryHasBatch = batch != null;
            }

            TransactionalBatch batch;
            Context context;
        }

        public class MyMessage : IMessage
        {
            public string Property { get; set; }
        }
    }
}