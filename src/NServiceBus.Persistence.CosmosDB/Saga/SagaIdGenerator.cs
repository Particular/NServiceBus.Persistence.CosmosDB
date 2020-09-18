namespace NServiceBus.Persistence.CosmosDB
{
    using System;

    using Sagas;

    class SagaIdGenerator : ISagaIdGenerator
    {
        public Guid Generate(SagaIdGeneratorContext context)
        {
            if (context.CorrelationProperty == SagaCorrelationProperty.None)
            {
                throw new Exception("The CosmosDB saga persister doesn't support custom saga finders.");
            }

            return CosmosDBSagaIdGenerator.Generate(context.SagaMetadata.SagaEntityType, context.CorrelationProperty.Name, context.CorrelationProperty.Value);
        }
    }
}