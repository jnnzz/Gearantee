using System;

namespace ASI.Basecode.Data.Models
{
    public class Reservation
    {
        public long ReservationId { get; set; }
        public long BorrowerProfileId { get; set; }
        public long EquipmentId { get; set; }
        public string ReviewedByUserId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime ReservationStart { get; set; }
        public DateTime ReservationEnd { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public string RejectionReason { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public BorrowerProfile BorrowerProfile { get; set; }
        public EquipmentItem EquipmentItem { get; set; }
        public ApplicationUser ReviewedByUser { get; set; }
        public ReleaseRecord ReleaseRecord { get; set; }
    }
}
