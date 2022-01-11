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
        public void UsePessimisticLocking() => PessimisticLockingEnabled = true;

        internal bool PessimisticLockingEnabled { get; private set; }
        internal bool MigrationModeEnabled { get; private set; }

        internal PessimisticLockingConfiguration PessimisticLockingConfiguration { get; private set; } = new PessimisticLockingConfiguration();
    }
}