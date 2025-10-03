namespace NServiceBus.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Microsoft.Azure.Cosmos;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;

[TestFixture]
public class When_default_container_and_extractor_is_configured : NServiceBusAcceptanceTest
{
    static string defaultContainerName = $"{SetupFixture.ContainerName}_default";
    Container defaultContainer;

    [SetUp]
    public async Task Setup()
    {
        await SetupFixture.CosmosDbClient.CreateDatabaseIfNotExistsAsync(SetupFixture.DatabaseName)
            .ConfigureAwait(false);

        Database database = SetupFixture.CosmosDbClient.GetDatabase(SetupFixture.DatabaseName);

        var containerProperties =
            new ContainerProperties(defaultContainerName, SetupFixture.PartitionPathKey)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(containerProperties)
            .ConfigureAwait(false);

        defaultContainer = database.GetContainer(defaultContainerName);
    }

    [TearDown]
    public new async Task Teardown()
    {
        await defaultContainer.DeleteContainerStreamAsync();
    }

    [Test]
    public async Task Should_overwrite_default_with_extractor_container()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint(new Endpoint(true), b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.That(context.Container.Id, Is.EqualTo(SetupFixture.ContainerName));
    }

    [Test]
    public async Task Should_not_overwrite_default_with_extractor_container()
    {
        var runSettings = new RunSettings();
        runSettings.DoNotRegisterDefaultPartitionKeyProvider();

        Context context = await Scenario.Define<Context>()
            .WithEndpoint(new Endpoint(false), b =>
            {
                b.When(s => s.SendLocal(new MyMessage()));
            })
            .Done(c => c.Done)
            .Run(runSettings);

        Assert.That(context.Container.Id, Is.EqualTo(defaultContainerName));
    }

    class Context : ScenarioContext
    {
        public bool Done { get; set; }
        public Container Container { get; set; }
    }

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint(bool enableContainerFromMessageExtractor) =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.EnableOutbox();
                config.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
                var persistence = config.UsePersistence<CosmosPersistence>();
                persistence.DefaultContainer(defaultContainerName, SetupFixture.PartitionPathKey);
                if (enableContainerFromMessageExtractor)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    persistence.EnableContainerFromMessageExtractor();
#pragma warning restore CS0618 // Type or member is obsolete
                }
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
    }

    class MyMessage : IMessage
    {
    }
}