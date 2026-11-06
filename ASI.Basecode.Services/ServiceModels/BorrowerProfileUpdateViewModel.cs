using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerProfileUpdateViewModel
    {
        [Required, StringLength(200)]
        public string Department { get; set; }

        [Phone, StringLength(50)]
        [Display(Name = "Contact number")]
        public string ContactNumber { get; set; }

        /// <summary>
        /// The profile's ConcurrencyStamp as rendered into the edit form. Compared with the
        /// stored value before writing so a stale form cannot overwrite a newer save.
        /// </summary>
        public string ConcurrencyStamp { get; set; }
    }
}
