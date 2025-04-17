namespace NServiceBus.Persistence.CosmosDB.Tests;

using NServiceBus;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void Approve()
    {
        string publicApi = typeof(CosmosPersistence).Assembly.GeneratePublicApi(new ApiGeneratorOptions { ExcludeAttributes = ["System.Runtime.Versioning.TargetFrameworkAttribute", "System.Reflection.AssemblyMetadataAttribute"] });
        Approver.Verify(publicApi);
    }
}