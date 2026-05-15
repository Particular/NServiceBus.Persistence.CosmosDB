namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using AcceptanceTesting;
using Microsoft.Azure.Cosmos;
using Persistence.CosmosDB;

class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromHeadersExtractor
{
    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
        return true;
    }
}

class ContainerInformationProvider : IContainerInformationFromHeadersExtractor
{
    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
        return true;
    }
}

class FaultyPartitionKeyProvider : IPartitionKeyFromHeadersExtractor
{
    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        partitionKey = null;
        return false;
    }
}

class FaultyContainerInformationProvider : IContainerInformationFromHeadersExtractor
{
    public bool TryExtract(IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        containerInformation = null;
        return false;
    }
}