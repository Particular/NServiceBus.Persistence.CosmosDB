namespace NServiceBus.Persistence.CosmosDB.Analyzers.Test;

using System.Threading.Tasks;
using NUnit.Framework;
//using static AzureFunctionsDiagnostics;

[TestFixture]
public class CosmosPersistenceConfigAnalyzerTests : AnalyzerTestFixture<ContainerExtractorConfigurationAnalyzer>
{
    [Test]
    public Task DiagnosticIsReportedEnableContainerFromMessageExtractor()
    {
        var source =
            $@"using NServiceBus;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using NServiceBus.Persistence.CosmosDB;
class Foo
{{
    void Direct(EndpointConfiguration endpointConfiguration)
    {{
        var persistence = endpointConfiguration
    .UsePersistence<CosmosPersistence>()
    .CosmosClient(new CosmosClient(""asdf""))
    .DatabaseName(""Database1"")
    .DefaultContainer(""DefaultContainer"", ""/messageId"");
persistence.EnableContainerFromMessageExtractor();
    }}
}}";

        return Assert("NSBC001", source);
    }
}