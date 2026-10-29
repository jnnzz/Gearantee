using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Returns
{
    public class ReturnRecordViewModel
    {
        [Required]
        public long ReleaseRecordId { get; set; }

        // Manila local time from a datetime-local input
        [Required(ErrorMessage = "Enter the date and time the item came back.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Returned at")]
        public DateTime? ActualReturnAt { get; set; }

        [Required(ErrorMessage = "Choose the condition found during inspection.")]
        [StringLength(50)]
        [Display(Name = "Condition")]
        public string ReturnedCondition { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose the item's status after inspection.")]
        [StringLength(50)]
        [Display(Name = "Resulting item status")]
        public string ResultingItemStatus { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Inspection notes")]
        public string Notes { get; set; }

        // Display only; never read from the form
        public ReturnRecordListItemViewModel Loan { get; set; }
    }

    public class ReturnRecordListItemViewModel
    {
        public long? ReturnRecordId { get; set; }
        public long ReleaseRecordId { get; set; }
        public long ReservationId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string BorrowerName { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;

        // Manila local times
        public DateTime ReleasedAtLocal { get; set; }
        public DateTime DueAtLocal { get; set; }
        public DateTime? ReturnedAtLocal { get; set; }

        public string ReturnedCondition { get; set; }
        public int? DaysLate { get; set; }

        // Borrowed, Due today, Overdue, or Returned
        public string DisplayStatus { get; set; } = string.Empty;

        public string ReservationCode => $"RSV-{ReservationId}";
    }

    public class ReturnRecordDetailsViewModel : ReturnRecordListItemViewModel
    {
        public string Location { get; set; } = string.Empty;
        public string ResultingItemStatus { get; set; } = string.Empty;
        public string Notes { get; set; }
        public string ReceivedByName { get; set; }
        public string PenaltyStatus { get; set; }

        // When the custodian saved it (Manila local)
        public DateTime RecordedAtLocal { get; set; }
        public bool IsBackdated { get; set; }
    }

    public class ReturnRecordTab
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReturnRecordIndexViewModel
    {
        public List<ReturnRecordListItemViewModel> Items { get; set; } = new List<ReturnRecordListItemViewModel>();
        public List<ReturnRecordTab> Tabs { get; set; } = new List<ReturnRecordTab>();
        public string Tab { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    public class ReturnRecordResult : Common.ServiceResult
    {
        public long? ReturnRecordId { get; set; }

        // Controller returns 404
        public bool NotFound { get; set; }

        public static ReturnRecordResult Fail(string message) =>
            new ReturnRecordResult { Message = message };

        public static ReturnRecordResult Missing() =>
            new ReturnRecordResult { NotFound = true, Message = "Loan not found." };

        public static ReturnRecordResult Success(string message, long returnRecordId) =>
            new ReturnRecordResult
            {
                Succeeded = true,
                Message = message,
                ReturnRecordId = returnRecordId
            };
    }
}
