using System;

namespace ASI.Basecode.Data.Models
{
    public class ReturnRecord
    {
        public long ReturnRecordId { get; set; }
        public long ReleaseRecordId { get; set; }
        public string ReceivedByUserId { get; set; }
        public DateTime ActualReturnAt { get; set; }
        public string ReturnedCondition { get; set; }
        public string ResultingItemStatus { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ReleaseRecord ReleaseRecord { get; set; }
        public ApplicationUser ReceivedByUser { get; set; }
        public LateReturn LateReturn { get; set; }
    }
}
