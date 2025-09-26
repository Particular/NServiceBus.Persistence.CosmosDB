namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_default_container_and_both_faulty_container_extractors_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Default_container_should_be_used()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithCustomExtractors>(b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.That(context.Container.Id, Is.EqualTo(SetupFixture.ContainerName));
    }

    public class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public Container Container { get; set; }
    }

    public class EndpointWithCustomExtractors : EndpointConfigurationBuilder
    {
        public EndpointWithCustomExtractors() =>
            EndpointSetup<DefaultServer>((config, r) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                PersistenceExtensions<CosmosPersistence> persistence = config.UsePersistence<CosmosPersistence>();
                persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);
                TransactionInformationConfiguration transactionInformation = persistence.TransactionInformation();
                transactionInformation.ExtractContainerInformationFromMessage(new CustomMessageExtractor());
                transactionInformation.ExtractContainerInformationFromHeaders(new CustomHeadersExtractor());
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
                context.Done = true;
                context.Container = session.Container;
                return Task.CompletedTask;
            }

            Context context;
            ICosmosStorageSession session;
        }

        public class CustomMessageExtractor() : IContainerInformationFromMessagesExtractor
        {
            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = null;
                return false;
            }
        }

        public class CustomHeadersExtractor() : IContainerInformationFromHeadersExtractor
        {
            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = null;
                return false;
            }
        }

    }

    class MyMessage : IMessage
    {
    }
}