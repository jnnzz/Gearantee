using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Reservations
{
    /// <summary>
    /// A reservation as the custodian sees it in the approval queue. Extends the borrower-facing
    /// row so the two screens share one projection and the same display labels.
    /// </summary>
    public class ReservationReviewListItemViewModel : ReservationListItemViewModel
    {
        public string BorrowerName { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;

        /// <summary>The request is still Pending and can be decided.</summary>
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }

        /// <summary>Approved and not yet released, so the handover can be recorded.</summary>
        public bool CanRelease { get; set; }
    }

    public class ReservationReviewDetailsViewModel : ReservationReviewListItemViewModel
    {
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Location { get; set; } = string.Empty;
        public string ImageUrl { get; set; }
        public string RejectionReason { get; set; }
        public string ReviewerName { get; set; }
        public DateTime? ReviewedAtLocal { get; set; }
        public DateTime? ReleasedAtLocal { get; set; }
        public string ReleaseNotes { get; set; }
    }

    public class ReservationReviewIndexViewModel
    {
        public List<ReservationReviewListItemViewModel> Items { get; set; } =
            new List<ReservationReviewListItemViewModel>();

        public List<ReservationStatusTab> Tabs { get; set; } = new List<ReservationStatusTab>();

        /// <summary>The active stored-status filter, or empty for all.</summary>
        public string StatusFilter { get; set; } = string.Empty;

        public string Search { get; set; } = string.Empty;

        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    /// <summary>Rejection form. The reason is required and matches the column width.</summary>
    public class RejectReservationViewModel
    {
        [Required, StringLength(1000)]
        [Display(Name = "Reason for rejection")]
        public string Reason { get; set; }
    }

    /// <summary>Release form. Notes are optional and match the column width.</summary>
    public class ReleaseReservationViewModel
    {
        [StringLength(1000)]
        [Display(Name = "Release notes (optional)")]
        public string Notes { get; set; }
    }
}
