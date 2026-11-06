using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReservationRepository
    {
        IQueryable<Reservation> GetReservations();
        void AddReservation(Reservation model);
        void UpdateReservation(Reservation model);

        /// <summary>
        /// Acquires submission locks inside the caller's active SERIALIZABLE transaction. A
        /// borrower-scoped gate prevents SQL Server range-lock deadlocks across that borrower's
        /// different items; a borrower/item lock protects the duplicate check. The caller must
        /// first acquire the shared equipment row lock so inventory and reservation workflows
        /// use the same lock order.
        /// </summary>
        void AcquireSubmissionLock(long borrowerProfileId, long equipmentId);
    }
}
