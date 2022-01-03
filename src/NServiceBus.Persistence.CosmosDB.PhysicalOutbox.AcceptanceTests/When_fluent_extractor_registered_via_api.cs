namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
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

                    return session.Send(new StartSaga1 { DataId = Guid.NewGuid() }, sendOptions);
                }))
                .Done(c => c.SagaReceivedMessage)
                .Run(runSettings);

            Assert.True(context.StateMatched);
        }

        public class Context : ScenarioContext
        {
            public bool SagaReceivedMessage { get; set; }
            public bool StateMatched { get; set; }
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
                        state.StateMatched = Guid.Parse(value).Equals(state.TestRunId);
                        return value;
                    }, new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)), (Context)r.ScenarioContext);

                    var persistence = config.UsePersistence<CosmosPersistence>();
                    persistence.ExtractWith(extractor);
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
                    Data.DataId = message.DataId;
                    testContext.SagaReceivedMessage = true;
                    MarkAsComplete();
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga1>(m => m.DataId).ToSaga(s => s.DataId);
                }

                readonly Context testContext;
            }

            public class CustomExtractor : IExtractTransactionInformationFromHeaders
            {
                readonly Context testContext;
                public CustomExtractor(Context testContext) => this.testContext = testContext;

                public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey,
                    out ContainerInformation? containerInformation)
                {
                    partitionKey = new PartitionKey(testContext.TestRunId.ToString());
                    containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                    testContext.StateMatched = true;
                    return true;
                }
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