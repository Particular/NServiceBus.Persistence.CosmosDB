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
        runSettings.DoNotRegisterDefaultContainerInformationProvider();

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
        if (!usePKExtractor)
        {
            Assert.That(context.PartitionKey, Is.EqualTo(new PartitionKey($"{context.EndpointName}-{context.MessageId}")));
        }
        else
        {
            Assert.That(context.PartitionKey, Is.EqualTo(new PartitionKey(context.TestRunId.ToString())));
        }
    }

    class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public Container Container { get; set; }
        public string MessageId { get; set; }
        public string EndpointName { get; set; }
        public PartitionKey PartitionKey { get; set; }
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
                context.PartitionKey = session.PartitionKey;
                context.Container = session.Container;
                context.MessageId = handlerContext.MessageId;
                context.Done = true;
                context.EndpointName = handlerContext.MessageHeaders["NServiceBus.ReplyToAddress"];
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