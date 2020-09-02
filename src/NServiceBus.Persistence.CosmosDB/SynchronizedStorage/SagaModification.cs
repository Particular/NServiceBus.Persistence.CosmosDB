namespace NServiceBus.Persistence.CosmosDB
{
    using Extensibility;
    using Sagas;

    abstract class SagaModification
    {
        public IContainSagaData SagaData { get; }
        public ContextBag Context { get; }

        protected SagaModification(IContainSagaData sagaData, ContextBag context)
        {
            SagaData = sagaData;
            Context = context;
        }
    }

    sealed class SagaSave : SagaModification
    {
        public SagaCorrelationProperty CorrelationProperty { get; }

        public SagaSave(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ContextBag context) : base(sagaData, context)
        {
            CorrelationProperty = correlationProperty;
        }
    }

    sealed class SagaUpdate : SagaModification
    {
        public SagaUpdate(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }
    }

    sealed class SagaDelete : SagaModification
    {
        public SagaDelete(IContainSagaData sagaData, ContextBag context) : base(sagaData, context)
        {
        }
    }
}