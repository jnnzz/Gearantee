using ASI.Basecode.Services.ServiceModels.Reservations;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationService
    {
        /// <summary>Returns null when the user may request equipment, otherwise the reason they can't.</summary>
        string CheckBorrowerEligibility(string userId);

        ReservationResult SubmitReservation(ReservationCreateViewModel model, string userId);

        /// <summary>The caller's own reservations, newest first. Status is a stored status, or empty for all.</summary>
        ReservationIndexViewModel GetMyReservations(string userId, string status, int page);

        /// <summary>Returns null when the reservation doesn't exist or belongs to someone else.</summary>
        ReservationDetailsViewModel RetrieveMyReservation(long reservationId, string userId);

        /// <summary>D4: the owner may cancel a Pending request, or an Approved one before the item is released.</summary>
        ReservationResult CancelReservation(long reservationId, string userId);

        /// <summary>
        /// The custodian review queue: every reservation, earliest window first.
        /// Status is a stored status (or empty for all); search matches item or borrower.
        /// Stale Pending requests are expired first, so the queue never shows a request
        /// whose start has already passed.
        /// </summary>
        ReservationReviewIndexViewModel GetReviewQueue(string status, string search, int page);

        /// <summary>The custodian's view of one reservation, or null when it doesn't exist.</summary>
        ReservationReviewDetailsViewModel RetrieveForReview(long reservationId);

        /// <summary>Approves a Pending request. Re-checks the window against the item's current bookings.</summary>
        ReservationResult ApproveReservation(string actorUserId, long reservationId);

        /// <summary>Rejects a Pending request. A reason is required.</summary>
        ReservationResult RejectReservation(string actorUserId, long reservationId, string reason);

        /// <summary>
        /// Records the handover: writes the ReleaseRecord and marks the item Borrowed.
        /// Requires an Approved request that has not been released yet.
        /// </summary>
        ReservationResult ReleaseEquipment(string actorUserId, long reservationId, string notes);
    }
}
