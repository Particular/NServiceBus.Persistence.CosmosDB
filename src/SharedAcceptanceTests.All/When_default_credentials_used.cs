namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Azure.Identity;
    using EndpointTemplates;
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    public class When_default_credentials_used : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            if (SetupFixture.IsRunningWithEmulator)
            {
                Assert.Ignore("This test uses DefaultAzureCredential which is not supported with the emulator.");
            }

            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointUsingDefaultCredentials>(b => b.When(session => session.SendLocal(new StartSaga1
                {
                    DataId = Guid.NewGuid()
                })))
                .Done(c => c.SagaReceivedMessage)
                .Run();

            Assert.True(context.SagaReceivedMessage);
        }

        public class Context : ScenarioContext
        {
            public bool SagaReceivedMessage { get; set; }
        }

        public class EndpointUsingDefaultCredentials : EndpointConfigurationBuilder
        {
            public EndpointUsingDefaultCredentials() =>
                EndpointSetup<DefaultServer>(config =>
                {
                    var builder = new DbConnectionStringBuilder
                    {
                        ConnectionString = SetupFixture.GetConnectionStringOrFallback()
                    };
                    builder.TryGetValue("AccountEndpoint", out var accountEndpoint);

                    var cosmosClient = new CosmosClient($"{accountEndpoint}", new DefaultAzureCredential(), new CosmosClientOptions());

                    var persistence = config.UsePersistence<CosmosPersistence>();
                    persistence.DisableContainerCreation();
                    persistence.CosmosClient(cosmosClient);
                    // with RBAC data plane operations are not supported, so we are using the existing database and container
                    persistence.DatabaseName(Environment.GetEnvironmentVariable("CosmosDBPersistence_ConnectionString_DatabaseName"));
                    persistence.DefaultContainer(Environment.GetEnvironmentVariable("CosmosDBPersistence_ConnectionString_ContainerOrTableName"), "/id");
                });

            public class JustASaga : Saga<JustASagaData>, IAmStartedByMessages<StartSaga1>
            {
                public JustASaga(Context testContext) => this.testContext = testContext;

                public Task Handle(StartSaga1 message, IMessageHandlerContext context)
                {
                    Data.DataId = message.DataId;
                    testContext.SagaReceivedMessage = true;
                    MarkAsComplete();
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper)
                    => mapper.ConfigureMapping<StartSaga1>(m => m.DataId).ToSaga(s => s.DataId);

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