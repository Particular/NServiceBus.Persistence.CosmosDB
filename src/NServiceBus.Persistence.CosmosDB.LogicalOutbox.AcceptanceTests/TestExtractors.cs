namespace NServiceBus.AcceptanceTests;

using System.Collections.Generic;
using AcceptanceTesting;
using Microsoft.Azure.Cosmos;
using Persistence.CosmosDB;

class PartitionKeyProvider(ScenarioContext scenarioContext) : IPartitionKeyFromMessageExtractor
{
    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        partitionKey = new PartitionKey(scenarioContext.TestRunId.ToString());
        return true;
    }
}

class ContainerInformationProvider : IContainerInformationFromMessagesExtractor
{
    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        containerInformation = new ContainerInformation(SetupFixture.ContainerName, new PartitionKeyPath(SetupFixture.PartitionPathKey));
        return true;
    }
}

class FaultyPartitionKeyProvider : IPartitionKeyFromMessageExtractor
{
    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out PartitionKey? partitionKey)
    {
        partitionKey = null;
        return false;
    }
}

class FaultyContainerInformationProvider : IContainerInformationFromMessagesExtractor
{
    public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
    {
        containerInformation = null;
        return false;
    }
}