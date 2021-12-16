namespace NServiceBus.AzureFunctions.SourceGenerator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using NUnit.Framework;
    using Particular.Approvals;
    using Persistence.CosmosDB.SourceGenerator;

    [TestFixture]
    public class SourceGeneratorApprovals
    {
        [Test]
        public void UsingNamespace()
        {
            var source =
@"using NServiceBus;

namespace Foo
{
    public class Startup
    {
        public const string EndpointName = ""endpoint"";
    }
}";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        [Test]
        public void UsingFullyQualifiedAttributeName()
        {
            var source =
@"

namespace Foo
{
    public class Startup
    {
        public const string EndpointName = ""endpoint"";
    }
}";
            var (output, _) = GetGeneratedOutput(source);

            Approver.Verify(output);
        }

        [OneTimeSetUp]
        public void Init()
        {
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

            var generator = new PartitionKeyMappingGenerator();

            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generateDiagnostics);

            // add necessary references for the generated trigger
            // references.Add(MetadataReference.CreateFromFile(typeof(ServiceBusTriggerAttribute).Assembly.Location));
            // references.Add(MetadataReference.CreateFromFile(typeof(ExecutionContext).Assembly.Location));
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