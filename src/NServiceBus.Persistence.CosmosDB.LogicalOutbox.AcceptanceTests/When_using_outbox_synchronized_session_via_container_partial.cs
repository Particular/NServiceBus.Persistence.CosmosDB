namespace NServiceBus.AcceptanceTests;

using Microsoft.Azure.Cosmos;
using NUnit.Framework;

public partial class When_using_outbox_synchronized_session_via_container
{
    partial void AssertPartitionPart(Context scenarioContext, string endpointName)
    {
        string partitionKeyPath = scenarioContext.PartitionKeyPath;
        Assert.Multiple(() =>
        {
            Assert.That(partitionKeyPath, Is.EqualTo(SetupFixture.PartitionPathKey));
            Assert.That(scenarioContext.PartitionKey, Is.EqualTo(new PartitionKey($"{endpointName}-{scenarioContext.TestRunId}")));
        });
    }
}