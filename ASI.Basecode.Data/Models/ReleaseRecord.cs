using System;

namespace ASI.Basecode.Data.Models
{
    public class ReleaseRecord
    {
        public long ReleaseRecordId { get; set; }
        public long ReservationId { get; set; }
        public string ReleasedByUserId { get; set; }
        public DateTime ActualReleaseAt { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Reservation Reservation { get; set; }
        public ApplicationUser ReleasedByUser { get; set; }
        public ReturnRecord ReturnRecord { get; set; }
    }
}
