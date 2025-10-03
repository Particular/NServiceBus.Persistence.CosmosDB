namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_no_default_container_and_both_container_extractors_configured : NServiceBusAcceptanceTest
{
    static string messageContainerName = $"{SetupFixture.ContainerName}_message";
    Container messageContainer;

    [SetUp]
    public async Task Setup()
    {
        await SetupFixture.CosmosDbClient.CreateDatabaseIfNotExistsAsync(SetupFixture.DatabaseName)
            .ConfigureAwait(false);

        Database database = SetupFixture.CosmosDbClient.GetDatabase(SetupFixture.DatabaseName);

        var messageContainerProperties =
            new ContainerProperties(messageContainerName, SetupFixture.PartitionPathKey)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(messageContainerProperties)
            .ConfigureAwait(false);

        messageContainer = database.GetContainer(messageContainerName);
    }

    [TearDown]
    public new async Task Teardown()
    {
        await messageContainer.DeleteContainerStreamAsync();
    }

    [Test]
    public async Task Message_extractor_should_be_used()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint(new EndpointWithCustomExtractors(true), b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.Multiple(() =>
        {
            Assert.That(context.MessageExtractorWasCalled, Is.True);
            Assert.That(context.Container.Id, Is.EqualTo(messageContainerName));
        });
    }

    [Test]
    public async Task Header_extractor_should_be_used()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultContainerInformationProvider();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint(new EndpointWithCustomExtractors(false), b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.Multiple(() =>
        {
            Assert.That(context.HeaderExtractorWasCalled, Is.True);
            Assert.That(context.Container.Id, Is.EqualTo(SetupFixture.ContainerName));
        });
    }

    public class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public Container Container { get; set; }
        public bool MessageExtractorWasCalled { get; set; }
        public bool HeaderExtractorWasCalled { get; set; }
    }

    public class EndpointWithCustomExtractors : EndpointConfigurationBuilder
    {
        public EndpointWithCustomExtractors(bool enableContainerFromMessageExtractor) =>
            EndpointSetup<DefaultServer>((config, r) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                var persistence = config.UsePersistence<CosmosPersistence>();
                if (enableContainerFromMessageExtractor)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    persistence.EnableContainerFromMessageExtractor();
#pragma warning restore CS0618 // Type or member is obsolete
                }
                var transactionInformation = persistence.TransactionInformation();
                transactionInformation.ExtractContainerInformationFromMessage(new CustomMessageExtractor((Context)r.ScenarioContext));
                transactionInformation.ExtractContainerInformationFromHeaders(new CustomHeadersExtractor((Context)r.ScenarioContext));
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

        public class CustomMessageExtractor(Context testContext) : IContainerInformationFromMessagesExtractor
        {
            public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = new ContainerInformation(messageContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                testContext.MessageExtractorWasCalled = true;
                return true;
            }
        }

        public class CustomHeadersExtractor(Context testContext) : IContainerInformationFromHeadersExtractor
        {
            public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
            {
                containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                testContext.HeaderExtractorWasCalled = true;
                return true;
            }
        }

    }

    class MyMessage : IMessage
    {
    }
}