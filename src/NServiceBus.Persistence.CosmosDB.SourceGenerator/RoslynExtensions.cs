namespace NServiceBus.Persistence.CosmosDB.SourceGenerator
{
    using Microsoft.CodeAnalysis;

    static class RoslynExtensions
    {
        public static INamedTypeSymbol? GetBestTypeByMetadataName(this Compilation compilation,
            string fullyQualifiedMetadataName)
        {
            // Try to get the unique type with this name, ignoring accessibility
            var type = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);

            // Otherwise, try to get the unique type with this name originally defined in 'compilation'
            type ??= compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
            return type;
        }
    }
}