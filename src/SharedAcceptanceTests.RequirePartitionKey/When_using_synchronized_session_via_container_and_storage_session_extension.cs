namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    [TestFixture]
    public class When_using_synchronized_session_via_container_and_storage_session_extension : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_commit_all_operations_using_the_same_batch()
        {
            TransactionalBatchCounterHandler.TotalTransactionalBatches = 0;

            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(s => s.SendLocal(new MyMessage())))
                .Done(c => c.FirstHandlerIsDone && c.SecondHandlerIsDone)
                .Run()
                .ConfigureAwait(false);

            Assert.AreEqual(1, TransactionalBatchCounterHandler.TotalTransactionalBatches, "Expected to have a single transactional batch but found more.");
        }

        public class Context : ScenarioContext
        {
            public const string Item1_Id = nameof(Item1_Id);
            public const string Item2_Id = nameof(Item2_Id);

            public bool FirstHandlerIsDone { get; set; }
            public bool SecondHandlerIsDone { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyHandlerUsingStorageSession : IHandleMessages<MyMessage>
            {
                public MyHandlerUsingStorageSession(ICosmosDBStorageSession session, Context context)
                {
                    this.session = session;
                    this.context = context;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
                {
                    session.Batch.CreateItem(new
                    {
                        id = Context.Item1_Id,
                        deep = new
                        {
                            down = context.TestRunId.ToString()
                        }
                    });

                    context.FirstHandlerIsDone = true;

                    return Task.CompletedTask;
                }

                Context context;
                ICosmosDBStorageSession session;
            }

            public class MyHandlerUsingExtensionMethod : IHandleMessages<MyMessage>
            {
                public MyHandlerUsingExtensionMethod(Context context)
                {
                    this.context = context;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
                {
                    var transactionalBatch = handlerContext.SynchronizedStorageSession.GetSharedTransactionalBatch();

                    transactionalBatch.CreateItem(new
                    {
                        id = Context.Item2_Id,
                        deep = new
                        {
                            down = context.TestRunId.ToString()
                        }
                    });

                    context.SecondHandlerIsDone = true;

                    return Task.CompletedTask;
                }

                Context context;
            }
        }

        public class MyMessage : IMessage
        {
            public string Property { get; set; }
        }
    }
}