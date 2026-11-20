using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels.BorrowingHistory
{
    // ★ WBS 53-55: GET inputs shared by Index and CSV/XLSX exports. The controller
    // ★ binds them; BorrowingHistoryService normalizes and validates before filtering.
    public class BorrowingHistoryFilterViewModel
    {
        public string Search { get; set; } = string.Empty;
        public string Borrower { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public long? CategoryId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LateReturn { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
    }

    // ★ WBS 52/55: one projected record used by the Razor table and both exporters.
    // ★ MapRow supplies derived status/late information and Manila-local dates.
    public class BorrowingHistoryRowViewModel
    {
        public long ReservationId { get; set; }
        public string ReservationCode => $"RSV-{ReservationId}";
        public string LoanCode { get; set; }
        public string BorrowerName { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAtLocal { get; set; }
        public DateTime StartLocal { get; set; }
        public DateTime DueLocal { get; set; }
        public bool IsFullDay { get; set; }
        public string StartLabel => Utilities.ReservationDates.StartLabel(StartLocal, IsFullDay);
        public string EndLabel => Utilities.ReservationDates.EndLabel(DueLocal, IsFullDay);
        public DateTime? ReleasedAtLocal { get; set; }
        public DateTime? ReturnedAtLocal { get; set; }
        public string ReturnedCondition { get; set; }
        public bool HasLateReturn { get; set; }
        public int DaysLate { get; set; }
    }

    public class BorrowingHistoryCategoryOption
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    // ★ WBS 52: the page receives its visible rows, selected filter values, dropdown
    // ★ choices and paging metadata together so navigation can preserve the current query.
    public class BorrowingHistoryIndexViewModel
    {
        public BorrowingHistoryFilterViewModel Filter { get; set; } = new();
        public List<BorrowingHistoryRowViewModel> Items { get; set; } = new();
        public List<BorrowingHistoryCategoryOption> Categories { get; set; } = new();
        public List<string> StatusOptions { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string ValidationMessage { get; set; }
    }
}
