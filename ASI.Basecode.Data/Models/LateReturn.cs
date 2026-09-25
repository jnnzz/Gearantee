using System;

namespace ASI.Basecode.Data.Models
{
    public class LateReturn
    {
        public long LateReturnId { get; set; }
        public long ReturnRecordId { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime ReturnedAt { get; set; }
        public int DaysLate { get; set; }
        public string PenaltyStatus { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ReturnRecord ReturnRecord { get; set; }
    }
}
