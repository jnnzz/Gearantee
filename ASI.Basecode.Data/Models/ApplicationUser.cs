using Microsoft.AspNetCore.Identity;
using System;

namespace ASI.Basecode.Data.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string UserCode { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public BorrowerProfile BorrowerProfile { get; set; }

        public string DisplayName => string.Join(" ", new[] { FirstName, LastName })
            .Trim();
    }
}
