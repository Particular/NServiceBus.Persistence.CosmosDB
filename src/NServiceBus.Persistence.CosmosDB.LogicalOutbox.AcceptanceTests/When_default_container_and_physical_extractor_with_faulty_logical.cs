namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NUnit.Framework;
using Persistence.CosmosDB;

public class When_default_container_and_physical_extractor_with_faulty_logical : NServiceBusAcceptanceTest
{
    static string logicalContainerName = $"{SetupFixture.ContainerName}_header";
    Container logicalContainer;

    [SetUp]
    public async Task Setup()
    {
        await SetupFixture.CosmosDbClient.CreateDatabaseIfNotExistsAsync(SetupFixture.DatabaseName)
            .ConfigureAwait(false);

        Database database = SetupFixture.CosmosDbClient.GetDatabase(SetupFixture.DatabaseName);

        var logicalContainerProperties =
            new ContainerProperties(logicalContainerName, SetupFixture.PartitionPathKey)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(logicalContainerProperties)
            .ConfigureAwait(false);

        logicalContainer = database.GetContainer(logicalContainerName);
    }

    [TearDown]
    public new async Task Teardown()
    {
        await logicalContainer.DeleteContainerStreamAsync();
    }

    [Test]
    public async Task Physical_extractor_should_be_used()
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

        Assert.That(context.Container.Id, Is.EqualTo(logicalContainerName));
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
                containerInformation = new ContainerInformation(logicalContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
                return true;
            }
        }

    }

    class MyMessage : IMessage
    {
    }
}