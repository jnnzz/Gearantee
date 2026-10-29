using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;

namespace ASI.Basecode.Data.Locks
{
    /// <summary>
    /// Centralized execution of SQL Server application locks (sys.sp_getapplock).
    /// Locks are owned by the ambient transaction and released when it ends.
    /// </summary>
    public class ApplicationLockManager : IApplicationLockManager
    {
        private const int DefaultLockTimeoutMs = 15000;

        private readonly AsiBasecodeDBContext _db;

        public ApplicationLockManager(AsiBasecodeDBContext db)
        {
            _db = db;
        }

        /// <inheritdoc />
        public void AcquireExclusive(
            string resource,
            int timeoutMs = DefaultLockTimeoutMs)
        {
            Acquire(resource, "Exclusive", timeoutMs);
        }

        /// <inheritdoc />
        public void AcquireShared(
            string resource,
            int timeoutMs = DefaultLockTimeoutMs)
        {
            Acquire(resource, "Shared", timeoutMs);
        }

        private void Acquire(
            string resource,
            string lockMode,
            int timeoutMs)
        {
            EnsureSerializableTransaction();
            _db.Database.ExecuteSqlInterpolated(
                $@"DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = {resource},
    @LockMode = {lockMode},
    @LockOwner = 'Transaction',
    @LockTimeout = {timeoutMs};
IF @lockResult < 0 THROW 51000, 'Could not acquire the application lock.', 1;");
        }

        private void EnsureSerializableTransaction()
        {
            var transaction = _db.Database.CurrentTransaction;
            if (transaction == null ||
                transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            {
                throw new InvalidOperationException(
                    "Application locks must be acquired inside an active SERIALIZABLE transaction.");
            }
        }
    }
}
