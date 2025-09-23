namespace NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

[TestFixture]
public class When_default_container_and_faulty_container_extractor_information_is_configured : NServiceBusAcceptanceTest
{
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_fallback_to_default_container(bool usePKExtractor)
    {
        var runSettings = new RunSettings();
        runSettings.RegisterFaultyContainerInformationProvider();

        if (!usePKExtractor)
        {
            runSettings.DoNotRegisterDefaultPartitionKeyProvider();
        }

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.That(context.Container.Id, Is.EqualTo(SetupFixture.ContainerName));
    }

    class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public Container Container { get; set; }
    }

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                PersistenceExtensions<CosmosPersistence> persistence = config.UsePersistence<CosmosPersistence>();
                persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);
            });

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public MyMessageHandler(ICosmosStorageSession session, Context context)
            {
                this.session = session;
                this.context = context;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext handlerContext)
            {
                context.Container = session.Container;
                context.Done = true;
                return Task.CompletedTask;
            }

            Context context;
            ICosmosStorageSession session;
        }
    }

    class MyMessage : IMessage
    {
    }
}