namespace NServiceBus.Persistence.CosmosDB;

using Extensibility;

class ContainerHolderResolver(IProvideCosmosClient provideCosmosClient, ContainerInformation? defaultContainerInformation, string databaseName)
{
    public ContainerHolder ResolveAndSetIfAvailable(ContextBag context)
    {
        if (context.TryGet<ContainerHolder>(out ContainerHolder containerHolder))
        {
            return containerHolder;
        }

        ContainerInformation? information;
        if (context.TryGet<ContainerInformation>(out ContainerInformation containerInformation))
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