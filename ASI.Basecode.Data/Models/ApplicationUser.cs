using Microsoft.AspNetCore.Identity;
using System;

namespace ASI.Basecode.Data.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string UserCode { get; set; }

        /// <summary>
        /// Canonical identity number for the user. Aliased to UserName so the identity
        /// login column and the human-facing ID number share a single source of truth.
        /// Not a physical column - it reads and writes UserName.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string IdNumber
        {
            get => UserName;
            set => UserName = value;
        }

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
