namespace NServiceBus.Persistence.CosmosDB;

using Extensibility;

class ContainerHolderResolver(IProvideCosmosClient provideCosmosClient, ContainerInformation? defaultContainerInformation, string databaseName, bool enableContainerFromMessageExtractor)
{
    public ContainerHolder ResolveAndSetIfAvailable(ContextBag context)
    {
        var hasContainerHolder = context.TryGet(out ContainerHolder containerHolder);
        var hasContainerInfoInContext = context.TryGet(out ContainerInformation containerInformation);

        // If a custom extractor has successfully extracted container information from the message or headers,
        // we always honor that over any existing container holder in context or the default container information.
        if (hasContainerInfoInContext && hasContainerHolder && enableContainerFromMessageExtractor)
        {
            if (containerInformation.ContainerName != containerHolder.Container.Id)
            {
                containerHolder = new ContainerHolder(provideCosmosClient.Client.GetContainer(databaseName, containerInformation.ContainerName), containerInformation.PartitionKeyPath);
                context.Set(containerHolder);
                return containerHolder;
            }
        }

        if (hasContainerHolder)
        {
            return containerHolder;
        }

        ContainerInformation? information = hasContainerInfoInContext ? containerInformation : defaultContainerInformation;

        if (!information.HasValue)
        {
            return null;
        }

        ContainerInformation informationValue = information.Value;
        containerHolder = new ContainerHolder(provideCosmosClient.Client.GetContainer(databaseName, informationValue.ContainerName), informationValue.PartitionKeyPath);
        context.Set(containerHolder);
        return containerHolder;
    }
}