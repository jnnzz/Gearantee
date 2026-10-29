using ASI.Basecode.Services.Common;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// Outcome of a borrower-profile write. Mirrors UserAdministrationResult / ReservationResult
    /// so the controller can tell "saved" apart from "someone else changed this row",
    /// "no such row" and "you may no longer do this" instead of collapsing everything into a bool.
    /// </summary>
    public sealed class BorrowerProfileResult : ServiceResult
    {
        private BorrowerProfileResult(
            bool succeeded,
            bool stale,
            bool missing,
            bool denied,
            string message)
        {
            Succeeded = succeeded;
            IsStale = stale;
            IsMissing = missing;
            IsDenied = denied;
            Message = message;
        }

        public bool IsStale { get; }
        public bool IsMissing { get; }

        /// <summary>The actor is no longer an active administrator holding borrower.manage.</summary>
        public bool IsDenied { get; }

        public static BorrowerProfileResult Success(string message) =>
            new BorrowerProfileResult(true, false, false, false, message);

        /// <summary>The form was rendered from an older version of the row.</summary>
        public static BorrowerProfileResult Stale(string message) =>
            new BorrowerProfileResult(false, true, false, false, message);

        public static BorrowerProfileResult Missing(string message) =>
            new BorrowerProfileResult(false, false, true, false, message);

        public static BorrowerProfileResult Denied(string message) =>
            new BorrowerProfileResult(false, false, false, true, message);

        public static BorrowerProfileResult Failure(string message) =>
            new BorrowerProfileResult(false, false, false, false, message);
    }
}
