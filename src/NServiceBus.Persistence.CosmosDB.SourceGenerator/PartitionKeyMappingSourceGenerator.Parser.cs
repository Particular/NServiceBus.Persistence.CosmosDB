namespace NServiceBus.Persistence.CosmosDB.SourceGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public sealed partial class PartitionKeyMappingSourceGenerator
    {
        sealed class Parser
        {
            const string PartitionKeyMapperBaseFullName = "NServiceBus.Persistence.CosmosDB.PartitionKeyMapperBase";
            readonly Compilation compilation;
            readonly PartitionKeyMapperSourceGenerationContext sourceGenerationContext;

            static DiagnosticDescriptor ContextClassesMustBePartial { get; } = new DiagnosticDescriptor(
                id: "ID",
                title: "",
                messageFormat: "",
                category: "Category",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            public Parser(Compilation compilation, in PartitionKeyMapperSourceGenerationContext sourceGenerationContext)
            {
                this.compilation = compilation;
                this.sourceGenerationContext = sourceGenerationContext;
            }

            public SourceGenerationSpec? GetGenerationSpec(IEnumerable<ClassDeclarationSyntax> classDeclarationSyntaxList)
            {
                INamedTypeSymbol? partitionKeyMapperBaseSymbol = compilation.GetBestTypeByMetadataName(PartitionKeyMapperBaseFullName);
                if (partitionKeyMapperBaseSymbol == null)
                {
                    return null;
                }

                foreach (ClassDeclarationSyntax classDeclarationSyntax in classDeclarationSyntaxList)
                {
                    CompilationUnitSyntax compilationUnitSyntax = classDeclarationSyntax.FirstAncestorOrSelf<CompilationUnitSyntax>()!;
                    SemanticModel compilationSemanticModel = compilation.GetSemanticModel(compilationUnitSyntax.SyntaxTree);

                    if (!DerivesFromPartitionKeyMapperBase(classDeclarationSyntax, partitionKeyMapperBaseSymbol,
                            compilationSemanticModel))
                    {
                        continue;
                    }

                    var visitor = new PropertyAndHeaderMappingSyntaxWalker();
                    visitor.Visit(classDeclarationSyntax);

                    var contextTypeSymbol = compilationSemanticModel.GetDeclaredSymbol(classDeclarationSyntax);

                    Location contextLocation = contextTypeSymbol!.Locations.Length > 0 ? contextTypeSymbol.Locations[0] : Location.None;

                    if (!TryGetClassDeclarationList(contextTypeSymbol, out string? classDeclaration))
                    {
                        // Class or one of its containing types is not partial so we can't add to it.
                        sourceGenerationContext.ReportDiagnostic(Diagnostic.Create(ContextClassesMustBePartial, contextLocation, new string[] { contextTypeSymbol.Name }));
                        continue;
                    }

                    return new SourceGenerationSpec(classDeclaration, contextTypeSymbol, contextLocation);
                }

                return null;
            }

            class PropertyAndHeaderMappingSyntaxWalker : CSharpSyntaxWalker
            {
                public List<(string typeName, string propertyName)> TypeAndProperty { get; } =
                    new List<(string typeName, string propertyName)>();

                public HashSet<string> HeaderNames { get; } = new HashSet<string>();

                public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
                {
                    if (node.Body is { Statements: { Count: > 0 } })
                    {
                        base.VisitConstructorDeclaration(node);
                    }
                }

                public override void VisitInvocationExpression(InvocationExpressionSyntax node)
                {
                    if (node.Expression is GenericNameSyntax nameSyntax && nameSyntax.TypeArgumentList.Arguments.Count == 1 && nameSyntax.Identifier.ValueText.StartsWith("ExtractFromMessage"))
                    {
                        if (node.ArgumentList.Arguments[0].Expression is SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax syntax })
                        {
                            TypeAndProperty.Add((nameSyntax.TypeArgumentList.Arguments[0].ToString(), syntax.Name.Identifier.ValueText));
                        }

                        return;
                    }

                    if (node.Expression is IdentifierNameSyntax identifierNameSyntax && identifierNameSyntax.Identifier.ValueText.StartsWith("ExtractFromHeader"))
                    {
                        if (node.ArgumentList.Arguments[0]
                                .Expression is LiteralExpressionSyntax literalExpressionSyntax)
                        {
                            HeaderNames.Add(literalExpressionSyntax.ToString());
                        }
                    }
                }
            }

            static bool TryGetClassDeclarationList(INamedTypeSymbol typeSymbol, out string? classDeclaration)
            {
                INamedTypeSymbol currentSymbol = typeSymbol;
                classDeclaration = null;

                while (currentSymbol != null)
                {
                    if (currentSymbol.DeclaringSyntaxReferences.First().GetSyntax() is ClassDeclarationSyntax classDeclarationSyntax)
                    {
                        SyntaxTokenList tokenList = classDeclarationSyntax.Modifiers;
                        int tokenCount = tokenList.Count;

                        bool isPartial = false;

                        string[] declarationElements = new string[tokenCount + 2];

                        for (int i = 0; i < tokenCount; i++)
                        {
                            SyntaxToken token = tokenList[i];
                            declarationElements[i] = token.Text;

                            if (token.IsKind(SyntaxKind.PartialKeyword))
                            {
                                isPartial = true;
                            }
                        }

                        if (!isPartial)
                        {
                            classDeclaration = null;
                            return false;
                        }

                        declarationElements[tokenCount] = "class";
                        declarationElements[tokenCount + 1] = currentSymbol.Name;

                        classDeclaration = string.Join(" ", declarationElements);
                        return true;
                    }

                    currentSymbol = currentSymbol.ContainingType;
                }

                return true;
            }

            // Returns true if a given type derives directly from PartitionKeyMapperBase.
            static bool DerivesFromPartitionKeyMapperBase(
                ClassDeclarationSyntax classDeclarationSyntax,
                INamedTypeSymbol partitionKeyMapperBaseSymbol,
                SemanticModel compilationSemanticModel)
            {
                SeparatedSyntaxList<BaseTypeSyntax>? baseTypeSyntaxList = classDeclarationSyntax.BaseList?.Types;
                if (baseTypeSyntaxList == null)
                {
                    return false;
                }

                INamedTypeSymbol? match = null;

                foreach (BaseTypeSyntax baseTypeSyntax in baseTypeSyntaxList)
                {
                    if (ModelExtensions.GetSymbolInfo(compilationSemanticModel, baseTypeSyntax.Type).Symbol is INamedTypeSymbol candidate && partitionKeyMapperBaseSymbol.Equals(candidate, SymbolEqualityComparer.Default))
                    {
                        match = candidate;
                        break;
                    }
                }

                return match != null;
            }

            internal static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax { BaseList: { Types: { Count: > 0 } } };
        }
    }
}