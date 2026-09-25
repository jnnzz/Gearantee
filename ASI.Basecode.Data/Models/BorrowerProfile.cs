using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class BorrowerProfile
    {
        public long BorrowerProfileId { get; set; }
        public string UserId { get; set; }
        public string SchoolId { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
        public bool IsEligible { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; }
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
