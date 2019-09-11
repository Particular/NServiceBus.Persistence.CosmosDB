namespace NServiceBus.Features
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Persistence.CosmosDB;
    using NServiceBus.Sagas;
    using Persistence;

    class SagaPersister : ISagaPersister
    {
        string connectionString;

        public SagaPersister(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, SynchronizedStorageSession session, ContextBag context)
        {
            throw new NotImplementedException();
        }

        public Task Update(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            throw new NotImplementedException();
        }

        public Task<TSagaData> Get<TSagaData>(Guid sagaId, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            throw new NotImplementedException();
        }

        public Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, SynchronizedStorageSession session, ContextBag context) where TSagaData : class, IContainSagaData
        {
            return Get<TSagaData>(SagaIdGenerator.Generate(typeof(TSagaData), propertyValue), session, context);
        }

        public Task Complete(IContainSagaData sagaData, SynchronizedStorageSession session, ContextBag context)
        {
            throw new NotImplementedException();
        }
    }
}