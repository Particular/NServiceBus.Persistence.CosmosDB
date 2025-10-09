namespace NServiceBus.Persistence.CosmosDB.Analyzers.Test;

using System.Threading.Tasks;
using NUnit.Framework;
//using static AzureFunctionsDiagnostics;

[TestFixture]
public class CosmosPersistenceConfigAnalyzerTests : AnalyzerTestFixture<ContainerExtractorConfigurationAnalyzer>
{
    [Test]
    public Task DiagnosticIsReportedWhenNoEnableContainerFromMessageExtractor()
    {
        var source = $$"""
            using NServiceBus;
            using System;
            using System.Threading.Tasks;
            using Microsoft.Azure.Cosmos;
            using NServiceBus.Persistence.CosmosDB;
            using System.Collections.Generic;

            class CustomContainerFromMessageExtractor : IContainerInformationFromMessagesExtractor
            {
                public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
                {
                    containerInformation = new ContainerInformation("TestContainer", new PartitionKeyPath("/key"));
                    return true;
                }
            }

            class Foo
            {
                void Direct(EndpointConfiguration endpointConfiguration)
                {
                    var persistence = endpointConfiguration
                        .UsePersistence<CosmosPersistence>()
                        .CosmosClient(new CosmosClient("asdf"))
                        .DatabaseName("Database1");

                    persistence.DefaultContainer("DefaultContainer", "/messageId");

                    var transactionInformation = persistence.TransactionInformation();
                    [|transactionInformation.ExtractContainerInformationFromMessage(new CustomContainerFromMessageExtractor())|];
                }
            }
        """;

        return Assert("NSBC001", source);
    }

    [Test]
    public Task DiagnosticIsReportedWhenNoEnableContainerFromMessageExtractorInExtension()
    {
        var source = $$"""
            using NServiceBus;
            using System;
            using System.Threading.Tasks;
            using Microsoft.Azure.Cosmos;
            using NServiceBus.Persistence.CosmosDB;
            using System.Collections.Generic;

            class CustomContainerFromMessageExtractor : IContainerInformationFromMessagesExtractor
            {
                public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
                {
                    containerInformation = new ContainerInformation("TestContainer", new PartitionKeyPath("/key"));
                    return true;
                }
            }

            public static class GGExtensions
            {
                public static void ExtractContainerInformationFromMessage(this PersistenceExtensions<CosmosPersistence> persistence)
                {
                    [|persistence.TransactionInformation().ExtractContainerInformationFromMessage(new CustomContainerFromMessageExtractor())|];
                }
            }

            class Foo
            {
                void Direct(EndpointConfiguration endpointConfiguration)
                {
                    var persistence = endpointConfiguration
                        .UsePersistence<CosmosPersistence>()
                        .CosmosClient(new CosmosClient("asdf"))
                        .DatabaseName("Database1");

                    persistence.DefaultContainer("DefaultContainer", "/messageId");
                    persistence.ExtractContainerInformationFromMessage();
                }
            }
         """;

        return Assert("NSBC001", source);
    }

    [Test]
    public Task DiagnosticIsNotReportedWhenEnableContainerFromMessageExtractor()
    {
        var source = $$"""
            using NServiceBus;
            using System;
            using System.Threading.Tasks;
            using Microsoft.Azure.Cosmos;
            using NServiceBus.Persistence.CosmosDB;
            using System.Collections.Generic;

            class CustomContainerFromMessageExtractor : IContainerInformationFromMessagesExtractor
            {
                public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
                {
                    containerInformation = new ContainerInformation("TestContainer", new PartitionKeyPath("/key"));
                    return true;
                }
            }

            class Foo
            {
                void Direct(EndpointConfiguration endpointConfiguration)
                {
                    var persistence = endpointConfiguration
                        .UsePersistence<CosmosPersistence>()
                        .CosmosClient(new CosmosClient("asdf"))
                        .DatabaseName("Database1")
                        .DefaultContainer("DefaultContainer", "/messageId");
                    persistence.EnableContainerFromMessageExtractor();

                    var transactionInformation = persistence.TransactionInformation();
                    transactionInformation.ExtractContainerInformationFromMessage(new CustomContainerFromMessageExtractor());
                }
            }
         """;

        return Assert("NSBC001", source);
    }

    [Test]
    public Task DiagnosticIsNotReportedWhenExtractContainerInformationFromMessageOnAnotherClass()
    {
        var source = $$"""
            using NServiceBus;
            using System;
            using System.Threading.Tasks;
            using Microsoft.Azure.Cosmos;
            using NServiceBus.Persistence.CosmosDB;
            using System.Collections.Generic;

            class CustomContainerFromMessageExtractor : IContainerInformationFromMessagesExtractor
            {
                public bool TryExtract(object message, IReadOnlyDictionary<string, string> headers, out ContainerInformation? containerInformation)
                {
                    containerInformation = new ContainerInformation("TestContainer", new PartitionKeyPath("/key"));
                    return true;
                }
            }

            class SomeOtherClass
            {
                internal void ExtractContainerInformationFromMessage() { }
            }

            class Foo
            {
                void Direct(EndpointConfiguration endpointConfiguration, SomeOtherClass otherClass)
                {
                    var persistence = endpointConfiguration
                        .UsePersistence<CosmosPersistence>()
                        .CosmosClient(new CosmosClient("asdf"))
                        .DatabaseName("Database1")
                        .DefaultContainer("DefaultContainer", "/messageId");
                    otherClass.ExtractContainerInformationFromMessage();
                }
            }
         """;

        return Assert("NSBC001", source);
    }
}