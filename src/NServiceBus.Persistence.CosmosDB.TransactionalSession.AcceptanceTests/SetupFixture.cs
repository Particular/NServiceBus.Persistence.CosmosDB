﻿namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using NServiceBus.AcceptanceTests;
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
        builder.AddCustomHandlers(new LoggingHandler());

        CosmosDbClient = builder.Build();

        var installer = new Installer(new CosmosClientProvidedByConfiguration { Client = CosmosDbClient }, new InstallerSettings
        {
            ContainerName = ContainerName,
            DatabaseName = DatabaseName,
            Disabled = false,
            PartitionKeyPath = PartitionPathKey
        });

        await installer.Install();

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
    public const string PartitionPathKey = $"/{PartitionPropertyName}";
    public const string PartitionPropertyName = "somekey";
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

class Installer(IProvideCosmosClient clientProvider, InstallerSettings settings)
{
    public async Task Install(CancellationToken cancellationToken = default)
    {
        if (settings == null || settings.Disabled)
        {
            return;
        }

        try
        {
            await CreateContainerIfNotExists(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!(e is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            log.Error("Could not complete the installation. ", e);
            throw;
        }
    }

    async Task CreateContainerIfNotExists(CancellationToken cancellationToken)
    {
        await clientProvider.Client.CreateDatabaseIfNotExistsAsync(settings.DatabaseName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Database database = clientProvider.Client.GetDatabase(settings.DatabaseName);

        var containerProperties =
            new ContainerProperties(settings.ContainerName, settings.PartitionKeyPath)
            {
                // in order for individual items TTL to work (example outbox records)
                DefaultTimeToLive = -1
            };

        await database.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    static ILog log = LogManager.GetLogger<Installer>();
}

class InstallerSettings
{
    public bool Disabled { get; set; }
    public string ContainerName { get; set; }
    public string PartitionKeyPath { get; set; }
    public string DatabaseName { get; set; }
}

class CosmosClientProvidedByConfiguration : IProvideCosmosClient
{
    public CosmosClient Client { get; set; }
}