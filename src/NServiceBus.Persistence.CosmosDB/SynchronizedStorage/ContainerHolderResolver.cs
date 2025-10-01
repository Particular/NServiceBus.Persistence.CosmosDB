namespace NServiceBus.Persistence.CosmosDB;

using Extensibility;

class ContainerHolderResolver(IProvideCosmosClient provideCosmosClient, ContainerInformation? defaultContainerInformation, string databaseName)
{
    public ContainerHolder ResolveAndSetIfAvailable(ContextBag context)
    {
        var hasContainerHolder = context.TryGet<ContainerHolder>(out ContainerHolder containerHolder);
        var hasContainerInfoInContext = context.TryGet<ContainerInformation>(out ContainerInformation containerInformation);

        // If a custom extractor has successfully extracted container information from the message or headers,
        // we always honor that over any existing container holder in context or the default container information.
        if (hasContainerInfoInContext && hasContainerHolder)
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

        ContainerInformation? information;

        if (hasContainerInfoInContext)
        {
            information = containerInformation;
        }
        else
        {
            information = defaultContainerInformation;
        }

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