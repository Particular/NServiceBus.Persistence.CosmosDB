namespace NServiceBus.AcceptanceTests;

using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Faults;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

[TestFixture]
public class When_faulty_pk_extractor_information_is_configured : NServiceBusAcceptanceTest
{
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_throw_meaningful_exception(bool useContainerExtractor)
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();
        runSettings.RegisterFaultyPartitionKeyProvider();

        if (!useContainerExtractor)
        {
            runSettings.DoNotRegisterDefaultContainerInformationProvider();
        }

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b =>
            {
                b.DoNotFailOnErrorMessages();
                if (!useContainerExtractor)
                {
                    b.CustomConfig(cfg =>
                    {
                        PersistenceExtensions<CosmosPersistence> persistence = cfg.UsePersistence<CosmosPersistence>();
                        persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);
                    });
                }
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.FailedMessages.Any())
            .Run(runSettings);

        FailedMessage failure = context.FailedMessages.FirstOrDefault()
            .Value.First();

        Assert.That(failure.Exception.Message, Does.Contain("partition key"));
    }

    class Context : ScenarioContext
    {
    }

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            });

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                Assert.Fail("Should not be called");
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}