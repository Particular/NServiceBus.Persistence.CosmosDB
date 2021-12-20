namespace NServiceBus.Persistence.CosmosDB.SourceGenerator
{
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;

    [Generator]
    public sealed partial class PartitionKeyExtractorSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxContextReceiver());
        }

        sealed class SyntaxContextReceiver : ISyntaxContextReceiver
        {
            public List<ClassDeclarationSyntax>? ClassDeclarationSyntaxList { get; private set; }

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (Parser.IsSyntaxTargetForGeneration(context.Node))
                {
                    (ClassDeclarationSyntaxList ??= new List<ClassDeclarationSyntax>()).Add((ClassDeclarationSyntax)context.Node);
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Short circuit if this is a different syntax receiver
            if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.ClassDeclarationSyntaxList == null)
            {
                // nothing to do yet
                return;
            }

            var sourceGenerationContext = new PartitionKeyMapperSourceGenerationContext(context);
            var parser = new Parser(context.Compilation, sourceGenerationContext);
            SourceGenerationSpec? spec = parser.GetGenerationSpec(receiver.ClassDeclarationSyntaxList);
            if (spec == null)
            {
                return;
            }

            var emitter = new Emitter(spec, sourceGenerationContext);
            emitter.Emit();
        }
    }

    readonly struct PartitionKeyMapperSourceGenerationContext
    {
        readonly GeneratorExecutionContext context;

        public PartitionKeyMapperSourceGenerationContext(GeneratorExecutionContext context) => this.context = context;

        public void ReportDiagnostic(Diagnostic diagnostic)
        {
            context.ReportDiagnostic(diagnostic);
        }

        public void AddSource(string hintName, SourceText sourceText)
        {
            context.AddSource(hintName, sourceText);
        }
    }
}