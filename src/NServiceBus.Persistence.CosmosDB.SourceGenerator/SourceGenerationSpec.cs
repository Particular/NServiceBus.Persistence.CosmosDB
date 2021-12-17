namespace NServiceBus.Persistence.CosmosDB.SourceGenerator
{
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    class SourceGenerationSpec
    {
        public SourceGenerationSpec(string? classDeclaration, HashSet<string> usings,
            INamedTypeSymbol contextTypeSymbol, Location location,
            List<string> typeNames)
        {
            ClassDeclaration = classDeclaration;
            Usings = usings;
            ContextTypeSymbol = contextTypeSymbol;
            Location = location;
            TypeNames = typeNames;
        }

        public string? ClassDeclaration { get; }
        public HashSet<string> Usings { get; }
        public INamedTypeSymbol ContextTypeSymbol { get; }
        public Location Location { get; }
        public List<string> TypeNames { get; }
    }
}