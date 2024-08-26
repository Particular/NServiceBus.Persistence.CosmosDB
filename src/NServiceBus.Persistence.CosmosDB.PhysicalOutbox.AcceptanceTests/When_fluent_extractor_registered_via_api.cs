namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    public class When_fluent_extractor_registered_via_api : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var runSettings = new RunSettings();
            runSettings.DoNotRegisterDefaultPartitionKeyProvider();
            runSettings.DoNotRegisterDefaultContainerInformationProvider();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithFluentExtractor>(b => b.When((session, ctx) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.SetHeader("PartitionKeyHeader", ctx.TestRunId.ToString());
                    sendOptions.SetHeader("ContainerNameHeader", ctx.ContainerName);

                    return session.Send(new StartSaga1 { DataId = Guid.NewGuid() }, sendOptions);
                }))
                .Done(c => c.SagaReceivedMessage)
                .Run(runSettings);

            Assert.That(context.PartitionHeaderStateMatched, Is.True);
            Assert.That(context.ContainerHeaderStateMatched, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool SagaReceivedMessage { get; set; }
            public bool PartitionHeaderStateMatched { get; set; }
            public bool ContainerHeaderStateMatched { get; set; }
            public string ContainerName { get; } = SetupFixture.ContainerName;
        }

        public class EndpointWithFluentExtractor : EndpointConfigurationBuilder
        {
            public EndpointWithFluentExtractor()
            {
                EndpointSetup<DefaultServer>((config, r) =>
                {
                    var persistence = config.UsePersistence<CosmosPersistence>();
                    var transactionInformation = persistence.TransactionInformation();
                    transactionInformation.ExtractPartitionKeyFromHeader("PartitionKeyHeader", (value, state) =>
                    {
                        state.PartitionHeaderStateMatched = Guid.Parse(value).Equals(state.TestRunId);
                        return new PartitionKey(value);
                    }, (Context)r.ScenarioContext);
                    transactionInformation.ExtractContainerInformationFromHeader("ContainerNameHeader", (value, state) =>
                    {
                        state.ContainerHeaderStateMatched = value.Equals(state.ContainerName);
                        return new ContainerInformation(value, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                    }, (Context)r.ScenarioContext);
                });
            }

            public class JustASaga : Saga<JustASagaData>, IAmStartedByMessages<StartSaga1>
            {
                public JustASaga(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(StartSaga1 message, IMessageHandlerContext context)
                {
                    testContext.SagaReceivedMessage = true;
                    MarkAsComplete();
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper) =>
                    mapper.ConfigureMapping<StartSaga1>(m => m.DataId).ToSaga(s => s.DataId);

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
    }
}