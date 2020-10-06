namespace NServiceBus.AcceptanceTests
{
    using Microsoft.Azure.Cosmos;
    using NUnit.Framework;

    public partial class When_using_outbox_synchronized_session_via_container
    {
        partial void AssertPartitionPart(Context scenarioContext)
        {
            string partitionKeyPath = scenarioContext.PartitionKeyPath;
            Assert.AreEqual(SetupFixture.PartitionPathKey, partitionKeyPath);
            Assert.AreEqual(new PartitionKey(scenarioContext.TestRunId.ToString()), scenarioContext.PartitionKey);
        }
    }
}