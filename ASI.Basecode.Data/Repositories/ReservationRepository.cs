using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Locks;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
        private readonly IApplicationLockManager _applicationLocks;

        public ReservationRepository(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
            _applicationLocks = new ApplicationLockManager(
                (AsiBasecodeDBContext)unitOfWork.Database);
        }

        public IQueryable<Reservation> GetReservations()
        {
            return this.GetDbSet<Reservation>();
        }

        public void AddReservation(Reservation model)
        {
            this.GetDbSet<Reservation>().Add(model);
            UnitOfWork.SaveChanges();
        }

        public void UpdateReservation(Reservation model)
        {
            this.GetDbSet<Reservation>().Update(model);
            UnitOfWork.SaveChanges();
        }

        public void AcquireSubmissionLock(long borrowerProfileId, long equipmentId)
        {
            var transaction = Context.Database.CurrentTransaction;
            if (transaction == null ||
                transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            {
                throw new InvalidOperationException(
                    "The reservation submission lock requires an active SERIALIZABLE transaction.");
            }

            if (Context.Database.IsSqlServer())
            {
                // Prevent serializable range-lock deadlocks across this borrower's different
                // items. The equipment row lock is already held, so the borrower gate can't
                // form a cycle with inventory updates. Retain a narrower lock for the duplicate
                // check; both locks release when the transaction ends.
                var borrowerResource = $"Gearantee.ReservationSubmitBorrower:{borrowerProfileId}";
                _applicationLocks.AcquireExclusive(borrowerResource);

                var resource = $"Gearantee.ReservationSubmit:{borrowerProfileId}:{equipmentId}";
                _applicationLocks.AcquireExclusive(resource);
            }
        }
    }
}
