using ASI.Basecode.Services.Common;

namespace ASI.Basecode.Services.ServiceModels.Reservations
{
    /// <summary>
    /// Outcome of an action on a reservation: a borrower action (submit, cancel) or a
    /// custodian decision (approve, reject, release).
    /// </summary>
    public class ReservationResult : ServiceResult
    {
        public long? ReservationId { get; set; }

        /// <summary>True when the reservation doesn't exist or isn't the caller's; the controller returns 404.</summary>
        public bool NotFound { get; set; }

        /// <summary>True when the caller no longer holds the permission the action requires.</summary>
        public bool Forbidden { get; set; }

        public static ReservationResult Fail(string message) =>
            new ReservationResult { Message = message };

        public static ReservationResult Missing() =>
            new ReservationResult { NotFound = true, Message = "Reservation not found." };

        public static ReservationResult Denied(string message) =>
            new ReservationResult { Forbidden = true, Message = message };

        public static ReservationResult Success(string message, long reservationId) =>
            new ReservationResult
            {
                Succeeded = true,
                Message = message,
                ReservationId = reservationId
            };
    }
}
