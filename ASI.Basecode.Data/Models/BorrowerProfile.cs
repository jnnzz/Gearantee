using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models
{
    public class BorrowerProfile
    {
        public long BorrowerProfileId { get; set; }
        public string UserId { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
        public bool IsEligible { get; set; }

        /// <summary>
        /// Optimistic-concurrency token for this profile row. Regenerated on every write and
        /// configured with IsConcurrencyToken, so a form that was opened before another
        /// administrator saved cannot silently overwrite the newer values. Initialised here so
        /// every creation path (registration, user administration, CSV import, seeding, tests)
        /// gets a value without further changes.
        /// </summary>
        public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; }
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
