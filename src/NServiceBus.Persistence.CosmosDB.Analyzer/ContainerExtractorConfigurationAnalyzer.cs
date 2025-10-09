namespace NServiceBus.Persistence.CosmosDB.Analyzers
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System.Collections.Immutable;
    using System.Linq;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ContainerExtractorConfigurationAnalyzer : DiagnosticAnalyzer
    {
        static string cosmosPersistenceExtension = "NServiceBus.PersistenceExtensions<NServiceBus.CosmosPersistence>";
        static string transactionInformationConfiguration = "NServiceBus.Persistence.CosmosDB.TransactionInformationConfiguration";

        public static readonly DiagnosticDescriptor MissingEnableContainerFromMessageExtractor = new DiagnosticDescriptor(
            "NSBC001",
            "EnableContainerFromMessageExtractor should be called when using both default container and message extractors",
            "The endpoint has both default container and message container extractors configured, but does not have EnableContainerFromMessageExtractor set. Consider calling persistence.EnableContainerFromMessageExtractor().",
            "Configuration",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "When configuring both a default container and container message extractors, you should call EnableContainerFromMessageExtractor to ensure the message extractors take precedence.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MissingEnableContainerFromMessageExtractor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
            //context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
            //context.RegisterSyntaxNodeAction(AnalyzeConstructorDeclaration, SyntaxKind.ConstructorDeclaration);
        }

        static void Analyze(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is InvocationExpressionSyntax invocationExpression))
            {
                return;
            }

            if (!(invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression))
            {
                return;
            }

            var methodName = memberAccessExpression.Name.Identifier.ValueText;

            // Check for container extractor methods
            if (!methodName.StartsWith("ExtractContainerInformationFromMessage") ||
                !IsCorrectObjectMethod(context, memberAccessExpression, transactionInformationConfiguration))
            {
                return;
            }

            // Get nearest enclosing block/body for the invocation.
            var body = GetContainingBody(invocationExpression);
            if (body == null)
            {
                // Could be an expression-bodied member or outside a block; nothing to analyze.
                return;
            }

            if (ContainerConfigRequiresWarning(context, body))
            {
                var diagnostic = Diagnostic.Create(
                        MissingEnableContainerFromMessageExtractor,
                        invocationExpression.GetLocation());

                context.ReportDiagnostic(diagnostic);
            }
        }

        static BlockSyntax GetContainingBody(SyntaxNode node)
        {
            if (node == null)
            {
                return null;
            }

            // Method body
            var methodDecl = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (methodDecl != null)
            {
                return methodDecl.Body;
            }

            // Constructor body
            var ctorDecl = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            if (ctorDecl != null)
            {
                return ctorDecl.Body;
            }

            // Accessor body (get/set)
            var accessor = node.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
            if (accessor != null)
            {
                return accessor.Body;
            }

            // Local function body
            var localFunc = node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
            if (localFunc != null)
            {
                return localFunc.Body;
            }

            // Anonymous method: `delegate(...) { ... }`
            var anonMethod = node.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>();
            if (anonMethod != null)
            {
                return anonMethod.Block;
            }

            // Parenthesized lambda: `(...) => { ... }`
            var parenLambda = node.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>();
            if (parenLambda != null)
            {
                return parenLambda.Block;
            }

            // Simple lambda: `x => { ... }` (Body can be ExpressionSyntax or BlockSyntax)
            var simpleLambda = node.FirstAncestorOrSelf<SimpleLambdaExpressionSyntax>();
            if (simpleLambda != null)
            {
                return simpleLambda.Body as BlockSyntax;
            }

            // No enclosing block found (could be in an expression-bodied member or top-level expression)
            return null;
        }

        static bool ContainerConfigRequiresWarning(SyntaxNodeAnalysisContext context, BlockSyntax body)
        {
            if (body == null)
            {
                return false;
            }

            var statements = body.Statements;

            var hasDefaultContainer = false;
            var hasEnableContainerFromMessageExtractor = false;

            while (!hasDefaultContainer && !hasEnableContainerFromMessageExtractor)
            {
                foreach (var statement in statements)
                {
                    var expressionStatements = statement.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>();

                    foreach (var invocation in expressionStatements)
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                        {
                            var methodName = memberAccess.Name.Identifier.ValueText;

                            // Check for DefaultContainer call on the correct type
                            if (methodName == "DefaultContainer")
                            {
                                if (IsCorrectObjectMethod(context, memberAccess, cosmosPersistenceExtension))
                                {
                                    hasDefaultContainer = true;
                                }
                            }
                            // Check for EnableContainerFromMessageExtractor call
                            else if (methodName == "EnableContainerFromMessageExtractor")
                            {
                                if (IsCorrectObjectMethod(context, memberAccess, cosmosPersistenceExtension))
                                {
                                    hasEnableContainerFromMessageExtractor = true;
                                }
                            }
                        }
                    }
                }
            }

            return hasDefaultContainer && !hasEnableContainerFromMessageExtractor;
        }

        //static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        //{
        //    var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        //    AnalyzeForContainerConfiguration(context, methodDeclaration.Body);
        //}

        //static void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context)
        //{
        //    var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;
        //    AnalyzeForContainerConfiguration(context, constructorDeclaration.Body);
        //}

        //static void AnalyzeForContainerConfiguration(SyntaxNodeAnalysisContext context, BlockSyntax body)
        //{
        //    if (body == null)
        //    {
        //        return;
        //    }

        //    var statements = body.Statements;

        //    var hasDefaultContainer = false;
        //    var hasContainerExtractor = false;
        //    var hasEnableContainerFromMessageExtractor = false;

        //    foreach (var statement in statements)
        //    {
        //        var expressionStatements = statement.DescendantNodesAndSelf()
        //            .OfType<InvocationExpressionSyntax>();

        //        foreach (var invocation in expressionStatements)
        //        {
        //            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        //            {
        //                var methodName = memberAccess.Name.Identifier.ValueText;

        //                // Check for DefaultContainer call on the correct type
        //                if (methodName == "DefaultContainer")
        //                {
        //                    if (IsCorrectObjectMethod(context, memberAccess, cosmosPersistenceExtension))
        //                    {
        //                        hasDefaultContainer = true;
        //                    }
        //                }
        //                // Check for container extractor methods
        //                else if (methodName.StartsWith("ExtractContainerInformationFromMessage"))
        //                {
        //                    if (IsCorrectObjectMethod(context, memberAccess, transactionInformationConfiguration))
        //                    {
        //                        hasContainerExtractor = true;
        //                    }
        //                }
        //                // Check for EnableContainerFromMessageExtractor call
        //                else if (methodName == "EnableContainerFromMessageExtractor")
        //                {
        //                    if (IsCorrectObjectMethod(context, memberAccess, cosmosPersistenceExtension))
        //                    {
        //                        hasEnableContainerFromMessageExtractor = true;
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    // Report diagnostic if we have both default container and extractors but no EnableContainerFromMessageExtractor
        //    if (hasDefaultContainer && hasContainerExtractor && !hasEnableContainerFromMessageExtractor)
        //    {
        //        var diagnostic = Diagnostic.Create(
        //            MissingEnableContainerFromMessageExtractor,
        //            body.GetLocation());

        //        context.ReportDiagnostic(diagnostic);
        //    }
        //}

        static bool IsCorrectObjectMethod(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memberAccess, string correctObject)
        {
            var semanticModel = context.SemanticModel;
            var type = semanticModel.GetTypeInfo(memberAccess.Expression);

            return type.Type.ToString() == correctObject;
        }
    }


}
