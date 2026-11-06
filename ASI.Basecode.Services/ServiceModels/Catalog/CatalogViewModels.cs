using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Catalog
{
    public class CatalogFilterViewModel
    {
        [StringLength(100)]
        [Display(Name = "Search")]
        public string Search { get; set; }

        [Display(Name = "Category")]
        public long? CategoryId { get; set; }

        /// <summary>Manila local time, as entered in a datetime-local input.</summary>
        [DataType(DataType.DateTime)]
        [Display(Name = "Available from")]
        public DateTime? From { get; set; }

        /// <summary>Manila local time, as entered in a datetime-local input.</summary>
        [DataType(DataType.DateTime)]
        [Display(Name = "Available until")]
        public DateTime? To { get; set; }

        public int Page { get; set; } = 1;

        public bool HasWindow => From.HasValue && To.HasValue;
    }

    public class CatalogItemViewModel
    {
        public long EquipmentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Location { get; set; } = string.Empty;
        public string ConditionStatus { get; set; } = string.Empty;
        public string ImageUrl { get; set; }

        /// <summary>"Available" or "Borrowed", computed from active loans, not the stored status.</summary>
        public string AvailabilityStatus { get; set; } = string.Empty;
    }

    public class CatalogCategoryOption
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class CatalogIndexViewModel
    {
        public CatalogFilterViewModel Filter { get; set; } = new CatalogFilterViewModel();
        public List<CatalogItemViewModel> Items { get; set; } = new List<CatalogItemViewModel>();
        public List<CatalogCategoryOption> Categories { get; set; } = new List<CatalogCategoryOption>();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        /// <summary>Set when the From/To window is invalid; the window filter is then ignored.</summary>
        public string WindowError { get; set; }
    }

    public class BookedWindowViewModel
    {
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public bool IsFullDay { get; set; }
        public string StartLabel => Utilities.ReservationDates.StartLabel(StartLocal, IsFullDay);
        public string EndLabel => Utilities.ReservationDates.EndLabel(EndLocal, IsFullDay);

        /// <summary>"Reserved" for approved bookings, "On loan" for released items.</summary>
        public string Label { get; set; } = string.Empty;
    }

    public class CatalogItemDetailsViewModel
    {
        public long EquipmentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Description { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string Location { get; set; } = string.Empty;
        public string ConditionStatus { get; set; } = string.Empty;
        public string ImageUrl { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
        public List<BookedWindowViewModel> BookedWindows { get; set; } = new List<BookedWindowViewModel>();
    }
}
