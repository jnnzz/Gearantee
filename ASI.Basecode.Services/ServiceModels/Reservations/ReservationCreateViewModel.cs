using ASI.Basecode.Services.ServiceModels.Catalog;
using System;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Reservations
{
    public class ReservationCreateViewModel
    {
        [Required]
        public long EquipmentId { get; set; }

        /// <summary>Philippine calendar date; the selected end date is inclusive.</summary>
        [Required(ErrorMessage = "Enter the start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start")]
        public DateTime? ReservationStart { get; set; }

        /// <summary>Philippine calendar date; the selected end date is inclusive.</summary>
        [Required(ErrorMessage = "Enter the end date.")]
        [DataType(DataType.Date)]
        [Display(Name = "End")]
        public DateTime? ReservationEnd { get; set; }

        [Required(ErrorMessage = "Tell the custodian what the equipment is for.")]
        [StringLength(1000)]
        [Display(Name = "Purpose")]
        public string Purpose { get; set; } = string.Empty;

        /// <summary>Item summary and booked times for display only; never read from the form.</summary>
        public CatalogItemDetailsViewModel Item { get; set; }
    }
}
