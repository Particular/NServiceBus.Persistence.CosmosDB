namespace NServiceBus.Persistence.CosmosDB.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    class SourceGenerationSpec
    {
        public SourceGenerationSpec(string? classDeclaration, INamedTypeSymbol contextTypeSymbol, Location location)
        {
            ClassDeclaration = classDeclaration;
            ContextTypeSymbol = contextTypeSymbol;
            Location = location;
        }

        public string? ClassDeclaration { get; }
        public INamedTypeSymbol ContextTypeSymbol { get; }
        public Location Location { get; }
    }
}