﻿namespace NServiceBus.AcceptanceTests;

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using NUnit.Framework;
using Persistence.CosmosDB;

[SetUpFixture]
public class SetupFixture
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        string connectionString = ConnectionStringHelper.GetConnectionStringOrFallback();

        ContainerName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}";

        var builder = new CosmosClientBuilder(connectionString);
        builder.AddCustomHandlers(new LoggingHandler(), new TransactionalBatchCounterHandler());

        CosmosDbClient = builder.Build();

        var installer = new Installer(new CosmosClientProvidedByConfiguration { Client = CosmosDbClient }, new InstallerSettings
        {
            ContainerName = ContainerName,
            DatabaseName = DatabaseName,
            Disabled = false,
            PartitionKeyPath = PartitionPathKey
        });

        await installer.Install("");

        Database database = CosmosDbClient.GetDatabase(DatabaseName);
        Container = database.GetContainer(ContainerName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Container.DeleteContainerStreamAsync();
        CosmosDbClient.Dispose();
    }

    public const string DatabaseName = "CosmosDBPersistence";
    public const string PartitionPathKey = "/deep/down";
    public static string ContainerName;
    public static CosmosClient CosmosDbClient;
    public static Container Container;
    static double totalRequestCharges;

    class LoggingHandler : RequestHandler
    {
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            string requestCharge = response.Headers["x-ms-request-charge"];
            await TestContext.Progress.WriteLineAsync($"Charged RUs:{requestCharge} for {request.Method.Method} {request.RequestUri} IsBatch:{request.Headers["x-ms-cosmos-is-batch-request"]}");
            totalRequestCharges += Convert.ToDouble(requestCharge, CultureInfo.InvariantCulture);

            await TestContext.Progress.WriteLineAsync($"Total charged RUs: {totalRequestCharges}");

            if ((int)response.StatusCode == 429)
            {
                await TestContext.Progress.WriteLineAsync("Request throttled.");
            }

            return response;
        }
    }
}