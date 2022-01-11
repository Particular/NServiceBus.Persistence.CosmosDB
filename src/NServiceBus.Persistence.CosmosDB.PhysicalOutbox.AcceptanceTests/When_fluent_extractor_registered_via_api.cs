namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;
    using Persistence.CosmosDB;

    public class When_fluent_extractor_registered_via_api : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var runSettings = new RunSettings();
            runSettings.DoNotRegisterDefaultPartitionKeyProvider();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithFluentExtractor>(b => b.When((session, ctx) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.SetHeader("PartitionKeyHeader", ctx.TestRunId.ToString());

                    return session.Send(new StartSaga1 { DataId = ctx.TestRunId }, sendOptions);
                }))
                .Done(c => c.SagaReceivedMessage)
                .Run(runSettings);

            Assert.True(context.HeaderStateMatched);
            Assert.True(context.MessageStateMatched);
        }

        public class Context : ScenarioContext
        {
            public bool SagaReceivedMessage { get; set; }
            public bool HeaderStateMatched { get; set; }
            public bool MessageStateMatched { get; set; }
        }

        public class EndpointWithFluentExtractor : EndpointConfigurationBuilder
        {
            public EndpointWithFluentExtractor()
            {
                EndpointSetup<DefaultServer>((config, r) =>
                {
                    var extractor = new TransactionInformationExtractor();
                    extractor.ExtractFromHeader("PartitionKeyHeader", (value, state) =>
                    {
                        state.HeaderStateMatched = Guid.Parse(value).Equals(state.TestRunId);
                        return value;
                    }, (Context)r.ScenarioContext, new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)));
                    extractor.ExtractFromMessage<CompleteSagaMessage, Context>((message, state) =>
                    {
                        state.MessageStateMatched = message.DataId.Equals(state.TestRunId);
                        return new PartitionKey(message.DataId.ToString());
                    }, (Context)r.ScenarioContext, new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)));

                    var persistence = config.UsePersistence<CosmosPersistence>();
                    persistence.ExtractWith(extractor);
                });
            }

            public class JustASaga : Saga<JustASagaData>, IAmStartedByMessages<StartSaga1>, IHandleMessages<CompleteSagaMessage>
            {
                public JustASaga(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(StartSaga1 message, IMessageHandlerContext context)
                {
                    Data.DataId = message.DataId;

                    return context.SendLocal(new CompleteSagaMessage { DataId = message.DataId });
                }

                public Task Handle(CompleteSagaMessage message, IMessageHandlerContext context)
                {
                    testContext.SagaReceivedMessage = true;
                    MarkAsComplete();
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga1>(m => m.DataId).ToSaga(s => s.DataId);
                    mapper.ConfigureMapping<CompleteSagaMessage>(m => m.DataId).ToSaga(s => s.DataId);
                }

                readonly Context testContext;
            }

            public class JustASagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }

        public class StartSaga1 : ICommand
        {
            public Guid DataId { get; set; }
        }

        public class CompleteSagaMessage : ICommand
        {
            public Guid DataId { get; set; }
        }
    }
}