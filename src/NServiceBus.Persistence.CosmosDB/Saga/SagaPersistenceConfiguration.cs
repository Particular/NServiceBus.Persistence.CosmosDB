namespace NServiceBus.Persistence.CosmosDB
{
    /// <summary>
    /// The saga persistence configuration options.
    /// </summary>
    public class SagaPersistenceConfiguration
    {
        /// <summary>
        /// Enable support for sagas migrated from other persistence technologies by querying the saga from storage using a migrated saga id.
        /// </summary>
        public void EnableMigrationMode() => MigrationModeEnabled = true;

        /// <summary>
        /// Enables default saga persistence pessimistic locking. Default to optimistic locking when not used.
        /// </summary>
        public PessimisticLockingConfiguration UsePessimisticLocking()
        {
            PessimisticLockingConfiguration.PessimisticLockingEnabled = true;
            return PessimisticLockingConfiguration;
        }

        internal bool MigrationModeEnabled { get; private set; }

        internal PessimisticLockingConfiguration PessimisticLockingConfiguration { get; } = new PessimisticLockingConfiguration();
    }
}