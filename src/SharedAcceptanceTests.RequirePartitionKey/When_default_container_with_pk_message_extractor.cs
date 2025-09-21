namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Faults;
using Microsoft.Azure.Cosmos;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Persistence.CosmosDB;
using NServiceBus.Settings;
using NUnit.Framework;

[TestFixture]
public class When_default_container_with_pk_message_extractor : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Pk_in_message_is_used()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.Multiple(() =>
        {
            Assert.That(context.Container.Id, Is.EqualTo(SetupFixture.ContainerName));
            Assert.That(context.PartitionKey, Is.EqualTo(new PartitionKey(context.TestRunId.ToString())));
        });
    }

    class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public string MessageId { get; set; }
        public string EndpointName { get; set; }
        public PartitionKey PartitionKey { get; set; }
        public Container Container { get; set; }
        public bool ExtractorWasCalled { get; set; }
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

        public class MyHandler : IHandleMessages<MyMessage>
        {
            public MyHandler(ICosmosStorageSession session, Context context)
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

        public class CustomExtractor(Context testContext) : IPartitionKeyFromHeadersExtractor
        {
            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                partitionKey = new PartitionKey(testContext.TestRunId.ToString());
                testContext.ExtractorWasCalled = true;
                return true;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}