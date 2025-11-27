namespace NServiceBus.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_custom_provider_registered : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_be_used()
    {
        Context context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithCustomProvider>(b => b.When(session => session.SendLocal(new StartSaga1 { DataId = Guid.NewGuid() })))
            .Done(c => c.SagaReceivedMessage)
            .Run();

        Assert.That(context.ProviderWasCalled, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool SagaReceivedMessage { get; set; }
        public bool ProviderWasCalled { get; set; }
    }

    public class EndpointWithCustomProvider : EndpointConfigurationBuilder
    {
        public EndpointWithCustomProvider() =>
            EndpointSetup<DefaultServer>(config =>
            {
                config.RegisterComponents(c =>
                    c.AddSingleton<IProvideCosmosClient>(b => new CustomProvider(b.GetService<Context>())));
            });

        public class JustASaga(Context testContext) : Saga<JustASagaData>, IAmStartedByMessages<StartSaga1>
        {
            public Task Handle(StartSaga1 message, IMessageHandlerContext context)
            {
                Data.DataId = message.DataId;
                testContext.SagaReceivedMessage = true;
                MarkAsComplete();
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<JustASagaData> mapper) =>
                mapper.MapSaga(saga => saga.DataId).ToMessage<StartSaga1>(msg => msg.DataId);
        }

        public class CustomProvider(Context testContext) : IProvideCosmosClient
        {
            public CosmosClient Client
            {
                get
                {
                    testContext.ProviderWasCalled = true;
                    return SetupFixture.CosmosDbClient;
                }
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