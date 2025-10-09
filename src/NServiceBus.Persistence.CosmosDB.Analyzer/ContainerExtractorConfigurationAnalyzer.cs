namespace NServiceBus.Persistence.CosmosDB.Analyzers
{
    using System.Collections.Immutable;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ContainerExtractorConfigurationAnalyzer : DiagnosticAnalyzer
    {
        static string cosmosPersistenceExtension = "NServiceBus.PersistenceExtensions<NServiceBus.CosmosPersistence>";
        static string transactionInformationConfiguration = "NServiceBus.Persistence.CosmosDB.TransactionInformationConfiguration";

        public static readonly DiagnosticDescriptor MissingEnableContainerFromMessageExtractor = new DiagnosticDescriptor(
            "NSBC001",
            "EnableContainerFromMessageExtractor should be called when using both default container and message extractors",
            "The endpoint has both default container and message container extractors configured, but does not have EnableContainerFromMessageExtractor set. Consider calling persistence.EnableContainerFromMessageExtractor().",
            "NServiceBus.Persistence.CosmosDB",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "When configuring both a default container and container message extractors, you should call EnableContainerFromMessageExtractor to ensure the message extractors take precedence.",
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MissingEnableContainerFromMessageExtractor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var state = new InvocationTracker();

                compilationContext.RegisterSyntaxNodeAction(
                    c => Analyze(c, state),
                    SyntaxKind.InvocationExpression);

                compilationContext.RegisterCompilationEndAction(c => OnCompilationEnd(c, state));
            });
        }

        class InvocationTracker
        {
            public bool FoundDefaultContainer { get; set; } = false;
            public bool FoundEnableContainerFromMessageExtractor { get; set; } = false;
            public bool FoundExtractContainerInformationFromMessage { get; set; } = false;
            public Location ExtractorLocation { get; set; }
        }

        static void Analyze(SyntaxNodeAnalysisContext context, InvocationTracker state)
        {
            //if (ShouldReportDiagnostic(state))
            //{
            // No need to continue analyzing if we already know we should report a diagnostic
            //    return;
            //}

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
            if (methodName.StartsWith("ExtractContainerInformationFromMessage") &&
                IsCorrectObjectMethod(context, memberAccessExpression, transactionInformationConfiguration))
            {
                state.FoundExtractContainerInformationFromMessage = true;
                state.ExtractorLocation = invocationExpression.GetLocation();
                return;
            }

            // Check for DefaultContainer call
            if (methodName == "DefaultContainer" &&
                IsCorrectObjectMethod(context, memberAccessExpression, cosmosPersistenceExtension))
            {
                state.FoundDefaultContainer = true;
                return;
            }

            // Check for EnableContainerFromMessageExtractor call
            if (methodName == "EnableContainerFromMessageExtractor" &&
                IsCorrectObjectMethod(context, memberAccessExpression, cosmosPersistenceExtension))
            {
                state.FoundEnableContainerFromMessageExtractor = true;
                return;
            }
        }

        static void OnCompilationEnd(CompilationAnalysisContext context, InvocationTracker state)
        {
            if (ShouldReportDiagnostic(state))
            {
                var diagnostic = Diagnostic.Create(
                        MissingEnableContainerFromMessageExtractor,
                        state.ExtractorLocation);

                context.ReportDiagnostic(diagnostic);
            }
        }

        static bool IsCorrectObjectMethod(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memberAccess, string correctObject)
        {
            var semanticModel = context.SemanticModel;
            var type = semanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);

            return type.Type?.ToString() == correctObject;
        }

        static bool ShouldReportDiagnostic(InvocationTracker state) =>
            state.FoundDefaultContainer &&
            state.FoundExtractContainerInformationFromMessage &&
            !state.FoundEnableContainerFromMessageExtractor;
    }
}
