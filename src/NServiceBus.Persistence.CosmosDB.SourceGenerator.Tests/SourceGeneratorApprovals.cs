namespace NServiceBus.Persistence.CosmosDB.SourceGenerator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using NUnit.Framework;
    using Particular.Approvals;
    using SourceGenerator;

    [TestFixture]
    public class SourceGeneratorApprovals
    {
        [Test]
        public void SimpleMappingToTypeInSameNamespace()
        {
            var source =
@"
namespace Some.Complex
{
    using System;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Persistence.CosmosDB;

    internal partial class PartitionKeyMapper : PartitionKeyExtractorBase
    {
        public PartitionKeyMapper()
        {
            ExtractFromMessage<IProvideOrderId>(x => new PartitionKey(x.OrderId));
        }
    }

    public interface IProvideOrderId
    {
        string OrderId { get; }
    }
    public class OrderAccepted : IProvideOrderId
    {
        public string OrderId { get; set; }
    }
}
";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        [Test]
        public void SimpleMappingToTypeInDifferentNamespace()
        {
            var source =
                @"
namespace Some.Complex
{
    using System;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Persistence.CosmosDB;
    using Some.Other.Namespace;

    internal partial class PartitionKeyMapper : PartitionKeyExtractorBase
    {
        public PartitionKeyMapper()
        {
            ExtractFromMessage<IProvideOrderId>(x => new PartitionKey(x.OrderId));
        }
    }
}
namespace Some.Other.Namespace
{
    public interface IProvideOrderId
    {
        string OrderId { get; }
    }
    public class OrderAccepted : IProvideOrderId
    {
        public string OrderId { get; set; }
    }
}
";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        [Test]
        public void MultipleMappingsInDifferentNamespace()
        {
            var source =
                @"
namespace Some.Complex
{
    using System;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Persistence.CosmosDB;
    using Some.Other.Namespace;
    using Yet.Another.Namespace;

    internal partial class PartitionKeyMapper : PartitionKeyExtractorBase
    {
        public PartitionKeyMapper()
        {
            ExtractFromMessage<IProvideOrderId>(x => new PartitionKey(x.OrderId));
            ExtractFromMessage<OrderDeclined>(x => new PartitionKey(x.OrderId));
        }
    }
}
namespace Some.Other.Namespace
{
    public interface IProvideOrderId
    {
        string OrderId { get; }
    }
    public class OrderAccepted : IProvideOrderId
    {
        public string OrderId { get; set; }
    }
}
namespace Yet.Another.Namespace
{
    using Some.Other.Namespace;

    public class OrderDeclined : IProvideOrderId
    {
        public string OrderId { get; set; }
    }
}
";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        [Test]
        public void SimpleMappingToTypeInDifferentNamespaceWithUsingOutside()
        {
            var source =
                @"
using Some.Other.Namespace;

namespace Some.Complex
{
    using System;
    using Microsoft.Azure.Cosmos;
    using NServiceBus.Persistence.CosmosDB;
    
    internal partial class PartitionKeyMapper : PartitionKeyExtractorBase
    {
        public PartitionKeyMapper()
        {
            ExtractFromMessage<IProvideOrderId>(x => new PartitionKey(x.OrderId));
        }
    }
}
namespace Some.Other.Namespace
{
    public interface IProvideOrderId
    {
        string OrderId { get; }
    }
    public class OrderAccepted : IProvideOrderId
    {
        public string OrderId { get; set; }
    }
}
";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        static (string output, ImmutableArray<Diagnostic> diagnostics) GetGeneratedOutput(string source, bool suppressGeneratedDiagnosticsErrors = false)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new List<MetadataReference>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }

            var compilation = Compile(new[]
            {
                syntaxTree
            }, references);

            var generator = new PartitionKeyExtractorSourceGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);

            // add necessary references for the generated trigger
            references.Add(MetadataReference.CreateFromFile(typeof(PartitionKeyExtractorBase).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(PartitionKey).Assembly.Location));
            // references.Add(MetadataReference.CreateFromFile(typeof(Message).Assembly.Location));
            // references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));
            Compile(outputCompilation.SyntaxTrees, references);

            if (!suppressGeneratedDiagnosticsErrors)
            {
                Assert.False(generateDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), "Failed: " + generateDiagnostics.FirstOrDefault()?.GetMessage());
            }

            return (outputCompilation.SyntaxTrees.Last().ToString(), generateDiagnostics);
        }

        static CSharpCompilation Compile(IEnumerable<SyntaxTree> syntaxTrees, IEnumerable<MetadataReference> references)
        {
            var compilation = CSharpCompilation.Create("result", syntaxTrees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Verify the code compiled:
            var compilationErrors = compilation
                .GetDiagnostics()
                .Where(d => d.Severity >= DiagnosticSeverity.Warning);
            Assert.IsEmpty(compilationErrors, compilationErrors.FirstOrDefault()?.GetMessage());

            return compilation;
        }
    }
}