using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using System;
using System.Linq.Expressions;

namespace ASI.Basecode.Services.Utilities
{
    /// <summary>
    /// The single availability rule for equipment. Item status is never "Reserved";
    /// whether an item is free is computed from approved reservations and active loans.
    /// Use these expressions inside EF queries so every screen and the approval
    /// recheck apply the same rule.
    /// </summary>
    public static class ReservationAvailability
    {
        private static readonly string[] ReservableStatuses =
        {
            DomainValues.EquipmentStatuses.Available,
            DomainValues.EquipmentStatuses.Borrowed
        };

        /// <summary>
        /// True when the reservation occupies the item during [startUtc, endUtc).
        /// Approved reservations block their window until the item is returned.
        /// An overdue active loan blocks every window, because the item is still out.
        /// Pending, Rejected, Cancelled, and Expired reservations never block.
        /// Windows that only touch (one ends exactly when the other starts) do not conflict.
        /// </summary>
        public static Expression<Func<Reservation, bool>> Blocks(
            DateTime startUtc,
            DateTime endUtc,
            DateTime nowUtc)
        {
            return reservation =>
                reservation.Status == DomainValues.ReservationStatuses.Approved &&
                (reservation.ReleaseRecord == null ||
                    reservation.ReleaseRecord.ReturnRecord == null) &&
                ((reservation.ReservationStart < endUtc &&
                    reservation.ReservationEnd > startUtc) ||
                 (reservation.ReleaseRecord != null &&
                    reservation.ReservationEnd <= nowUtc));
        }

        /// <summary>
        /// True when the item may be offered to borrowers: not archived, in an active
        /// category, and not marked Under Maintenance, Unavailable, or Archived.
        /// A Borrowed item stays reservable for windows after its current loan.
        /// </summary>
        public static Expression<Func<EquipmentItem, bool>> IsReservable()
        {
            return item =>
                !item.IsArchived &&
                item.Category.IsActive &&
                (item.ItemStatus == DomainValues.EquipmentStatuses.Available ||
                    item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed);
        }

        /// <summary>
        /// Checks a requested window entered in Manila local time.
        /// Returns null when the window is valid, otherwise a message for the borrower.
        /// </summary>
        public static string ValidateWindow(DateTime? startLocal, DateTime? endLocal, DateTime nowUtc)
        {
            if (!startLocal.HasValue || !endLocal.HasValue)
            {
                return "Enter both a start and an end date and time.";
            }

            if (startLocal.Value >= endLocal.Value)
            {
                return "The end date and time must be after the start.";
            }

            if (ManilaClock.ToUtc(startLocal.Value) <= nowUtc)
            {
                return "The start date and time must be in the future.";
            }

            return null;
        }

        public static bool IsReservableStatus(string itemStatus)
        {
            return Array.IndexOf(ReservableStatuses, itemStatus) >= 0;
        }
    }
}
