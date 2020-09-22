namespace AzureStoragePersistenceSagaExporter.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Particular.AzureStoragePersistenceSagaExporter;

    class MigrationEndToEnd : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Can_migrate_from_ASP_to_CosmosDB()
        {
            var account = CloudStorageAccount.Parse(AzureStoragePersistenceConnectionString);
            var client = account.CreateCloudTableClient();

            var table = client.GetTableReference(typeof(MigratingEndpoint.MigratingSagaData).Name);

            await table.CreateIfNotExistsAsync();

            var testContext = await Scenario.Define<Context>(c => c.MyId = Guid.NewGuid())
                .WithEndpoint<MigratingEndpoint>(b => b.CustomConfig(ec =>
                {
                    ec.UsePersistence<AzureStoragePersistence>()
                    .ConnectionString(AzureStoragePersistenceConnectionString);
                }).When((s, c) => s.SendLocal(new StartSaga
                {
                    MyId = c.MyId
                })))
                .Done(ctx => ctx.CompleteSagaRequestSent)
                .Run();

            var workingDir = Path.GetTempPath();
            Directory.CreateDirectory(workingDir);

            var logger = new ConsoleLogger(true);

            await Exporter.Run(logger, AzureStoragePersistenceConnectionString, typeof(MigratingEndpoint.MigratingSagaData).Name, workingDir, CancellationToken.None);

            var newId = SagaIdGenerator.Generate(typeof(MigratingEndpoint.MigratingSagaData).FullName, nameof(MigratingEndpoint.MigratingSagaData.MyId), testContext.MyId.ToString());

            var filePath = Path.Combine(workingDir, typeof(MigratingEndpoint.MigratingSagaData).Name, $"{newId}.json");

            Assert.IsTrue(File.Exists(filePath), "File exported");

            var container = CosmosClient.GetContainer(DatabaseName, ContainerName);

            var partitionKey = Path.GetFileNameWithoutExtension(filePath);

            using (var stream = File.OpenRead(filePath))
            {
                var response = await container.CreateItemStreamAsync(stream, new Microsoft.Azure.Cosmos.PartitionKey(partitionKey));

                Assert.IsTrue(response.IsSuccessStatusCode, "Successfully imported");
            }

            await Scenario.Define<Context>(c => c.MyId = testContext.MyId)
                .WithEndpoint<MigratingEndpoint>(b => b.CustomConfig(ec =>
                {
                    ec.UsePersistence<CosmosDbPersistence>()
                    .CosmosClient(CosmosClient)
                    .DatabaseName(DatabaseName)
                    .DefaultContainer(ContainerName, PartitionPathKey)
                    .EnableMigrationMode();
                }))
                .WithEndpoint<SomeOtherEndpoint>()
                .Done(ctx => ctx.CompleteSagaResponseReceived)
                .Run();

            await table.DeleteAsync();
        }

        public class Context : ScenarioContext
        {
            public bool CompleteSagaRequestSent { get; set; }
            public bool CompleteSagaResponseReceived { get; set; }
            public Guid MyId { get; internal set; }
        }

        public class MigratingEndpoint : EndpointConfigurationBuilder
        {
            public MigratingEndpoint()
            {
                EndpointSetup<BaseEndpoint>();
            }

            public class MigratingSaga : Saga<MigratingSagaData>,
                IAmStartedByMessages<StartSaga>,
                IHandleMessages<CompleteSagaResponse>
            {
                readonly Context testContext;

                public MigratingSaga(Context testContext)
                {
                    this.testContext = testContext;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MigratingSagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(msg => msg.MyId).ToSaga(saga => saga.MyId);
                }

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    Data.MyId = message.MyId;
                    testContext.CompleteSagaRequestSent = true;
                    return context.Send($"{nameof(MigrationEndToEnd)}.{nameof(SomeOtherEndpoint)}", new CompleteSagaRequest());
                }

                public Task Handle(CompleteSagaResponse message, IMessageHandlerContext context)
                {
                    testContext.CompleteSagaResponseReceived = true;
                    MarkAsComplete();
                    return Task.CompletedTask;
                }
            }

            public class MigratingSagaData : ContainSagaData
            {
                public Guid MyId { get; set; }
            }
        }

        public class SomeOtherEndpoint : EndpointConfigurationBuilder
        {
            public SomeOtherEndpoint()
            {
                EndpointSetup<BaseEndpoint>(c => c.UsePersistence<InMemoryPersistence>());
            }

            public class CompleteSagaRequestHandler : IHandleMessages<CompleteSagaRequest>
            {
                public Task Handle(CompleteSagaRequest message, IMessageHandlerContext context)
                {
                    return context.Reply(new CompleteSagaResponse());
                }
            }
        }

        public class StartSaga : ICommand
        {
            public Guid MyId { get; set; }
        }

        public class CompleteSagaRequest : IMessage
        {
        }

        public class CompleteSagaResponse : IMessage
        {
        }
    }
}
