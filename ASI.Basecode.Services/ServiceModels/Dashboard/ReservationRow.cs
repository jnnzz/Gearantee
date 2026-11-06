using System;

namespace ASI.Basecode.Services.ServiceModels.Dashboard
{
    public class ReservationRow
    {
        public long ReservationId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string BorrowerName { get; set; }
        public string SchoolId { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public bool IsFullDay { get; set; }
        public string StartLabel => Utilities.ReservationDates.StartLabel(Utilities.ManilaClock.ToLocal(StartUtc), IsFullDay);
        public string EndLabel => Utilities.ReservationDates.EndLabel(Utilities.ManilaClock.ToLocal(EndUtc), IsFullDay);
        public DateTime RequestedAtUtc { get; set; }
        public string Status { get; set; }
        public string DisplayStatus { get; set; }
        public string RejectionReason { get; set; }
        public string Purpose { get; set; }
        public string ReviewerName { get; set; }
        public string CategoryName { get; set; }
        public string LocationName { get; set; }
        public string ImageUrl { get; set; }
        public long? ReleaseRecordId { get; set; }
        public DateTime? ReleasedAtUtc { get; set; }
        public DateTime? ReturnedAtUtc { get; set; }
        public string ReturnedCondition { get; set; }
        public int DaysOverdue { get; set; }

        public string ReservationCode => $"RSV-{ReservationId}";
        public string LoanCode => ReleaseRecordId is long id ? $"LN-{id}" : null;
    }
}
