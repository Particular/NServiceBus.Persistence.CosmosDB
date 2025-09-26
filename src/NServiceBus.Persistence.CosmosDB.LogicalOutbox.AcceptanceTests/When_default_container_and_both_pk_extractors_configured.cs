namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_default_container_and_both_pk_extractors_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Message_extractor_should_be_used()
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

        Assert.Multiple(() =>
        {
            Assert.That(context.MessageExtractorWasCalled, Is.True);
            Assert.That(context.PartitionKey, Is.EqualTo(new PartitionKey($"message-{context.TestRunId}")));
        });
    }

    public class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public PartitionKey PartitionKey { get; set; }
        public bool MessageExtractorWasCalled { get; set; }
        public bool HeaderExtractorWasCalled { get; set; }
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
                transactionInformation.ExtractPartitionKeyFromMessages(new CustomMessageExtractor((Context)r.ScenarioContext));
                transactionInformation.ExtractPartitionKeyFromHeaders(new CustomHeadersExtractor((Context)r.ScenarioContext));
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
                context.PartitionKey = session.PartitionKey;
                return Task.CompletedTask;
            }

            Context context;
            ICosmosStorageSession session;
        }

        public class CustomMessageExtractor(Context testContext) : IPartitionKeyFromMessageExtractor
        {
            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                partitionKey = new PartitionKey($"message-{testContext.TestRunId}");
                testContext.MessageExtractorWasCalled = true;
                return true;
            }
        }

        public class CustomHeadersExtractor(Context testContext) : IPartitionKeyFromHeadersExtractor
        {
            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
            {
                partitionKey = new PartitionKey($"header-{testContext.TestRunId}");
                testContext.HeaderExtractorWasCalled = true;
                return true;
            }
        }

    }

    class MyMessage : IMessage
    {
    }
}