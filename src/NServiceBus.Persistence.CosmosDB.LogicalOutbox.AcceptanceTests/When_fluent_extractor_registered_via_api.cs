﻿namespace NServiceBus.AcceptanceTests
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
                    session.SendLocal(new StartSaga1 { DataId = Guid.NewGuid(), PartitionKey = ctx.TestRunId })))
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
                    var persistence = config.UsePersistence<CosmosPersistence>();
                    var transactionInformation = persistence.TransactionInformation();
                    transactionInformation.ExtractFromMessage<StartSaga1, Context>((startSaga, state) =>
                    {
                        state.StateMatched = startSaga.PartitionKey.Equals(state.TestRunId);
                        return new PartitionKey(startSaga.PartitionKey.ToString());
                    }, (Context)r.ScenarioContext, new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey)));
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

            public class JustASagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }

        public class StartSaga1 : ICommand
        {
            public Guid DataId { get; set; }
            public Guid PartitionKey { get; set; }
        }
    }
}