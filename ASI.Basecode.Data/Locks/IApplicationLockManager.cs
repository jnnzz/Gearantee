namespace ASI.Basecode.Data.Locks
{
    /// <summary>
    /// Encapsulates SQL Server application-lock (sys.sp_getapplock) acquisition so raw
    /// lock SQL stays in the data layer.
    /// </summary>
    public interface IApplicationLockManager
    {
        /// <summary>
        /// Acquires an exclusive, transaction-owned application lock, blocking up to
        /// timeoutMs. Throws when the lock cannot be acquired.
        /// </summary>
        void AcquireExclusive(string resource, int timeoutMs = 15000);

        /// <summary>
        /// Acquires a shared, transaction-owned application lock, blocking up to timeoutMs.
        /// Concurrent shared holders are allowed; an exclusive holder still blocks them.
        /// Throws when the lock cannot be acquired.
        /// </summary>
        void AcquireShared(string resource, int timeoutMs = 15000);
    }
}
