namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using Extensibility;
    using Microsoft.Azure.Cosmos;
    using Sagas;

    abstract class SagaModification : Modification
    {
        public IContainSagaData SagaData { get; }

        protected SagaModification(IContainSagaData sagaData, ContextBag context) : base(context)
        {
            SagaData = sagaData;
        }

        public override void Success(TransactionalBatchOperationResult result)
        {
            Context.Set($"cosmos_etag:{SagaData.Id}", result.ETag);
        }
    }

    sealed class SagaSave : SagaModification
    {
        public SagaCorrelationProperty CorrelationProperty { get; }

        public SagaSave(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ContextBag context) : base(sagaData, context)
        {
            CorrelationProperty = correlationProperty;
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' could not be created possibly due to a concurrency conflict.");
        }
    }

    sealed class SagaUpdate : SagaModification
    {
        public SagaUpdate(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' was updated by another process or no longer exists.");
        }
    }

    sealed class SagaDelete : SagaModification
    {
        public SagaDelete(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }

        public override void Conflict(TransactionalBatchOperationResult result)
        {
            throw new Exception($"The '{SagaData.GetType().Name}' saga with id '{SagaData.Id}' can't be completed because it was updated by another process.");
        }
    }
}