using System.Threading.Tasks;
using NServiceBus.Extensibility;

namespace NServiceBus.Persistence.ComponentTests
{
    class SynchronizedStorageForTesting : ISynchronizedStorage
    {
        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
        {
            return Task.FromResult<CompletableSynchronizedStorageSession>(new CompletableSynchronizedStorageSessionForTesting());
        }

        class CompletableSynchronizedStorageSessionForTesting : CompletableSynchronizedStorageSession
        {
            public Task CompleteAsync()
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}