using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels.Reservations
{
    public class ReservationListItemViewModel
    {
        public long ReservationId { get; set; }
        public long EquipmentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>Manila local time.</summary>
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public bool IsFullDay { get; set; }
        public string StartLabel => Utilities.ReservationDates.StartLabel(StartLocal, IsFullDay);
        public string EndLabel => Utilities.ReservationDates.EndLabel(EndLocal, IsFullDay);
        public DateTime RequestedAtLocal { get; set; }

        /// <summary>The stored status: Pending, Approved, Rejected, Cancelled, or Expired.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>What the borrower sees, using the dashboard's labels (Awaiting Release, Borrowed, Overdue, ...).</summary>
        public string DisplayStatus { get; set; } = string.Empty;

        public bool CanCancel { get; set; }

        public string ReservationCode => $"RSV-{ReservationId}";
    }

    public class ReservationDetailsViewModel : ReservationListItemViewModel
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Location { get; set; } = string.Empty;
        public string ImageUrl { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string RejectionReason { get; set; }
        public string ReviewerName { get; set; }
        public DateTime? ReviewedAtLocal { get; set; }
        public DateTime? CancelledAtLocal { get; set; }
        public DateTime? ReleasedAtLocal { get; set; }
        public DateTime? ReturnedAtLocal { get; set; }
        public string ReturnedCondition { get; set; }
    }

    public class ReservationStatusTab
    {
        /// <summary>Query value; empty for "All".</summary>
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReservationIndexViewModel
    {
        public List<ReservationListItemViewModel> Items { get; set; } = new List<ReservationListItemViewModel>();
        public List<ReservationStatusTab> Tabs { get; set; } = new List<ReservationStatusTab>();

        /// <summary>The active stored-status filter, or empty for all.</summary>
        public string StatusFilter { get; set; } = string.Empty;
        public string Search { get; set; } = string.Empty;

        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }
}
