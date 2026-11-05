using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.AvailabilityCalendar
{
    public class AvailabilityCalendarFilterViewModel
    {
        // Keep complete weeks within the API range and avoid the grid's 1900 offset for years below 100.
        public const string MinDate = "0100-01-04";
        public const string MaxDate = "9999-12-26";

        [Range(1, long.MaxValue)]
        public long? CategoryId { get; set; }

        [Range(1, long.MaxValue)]
        public long? EquipmentId { get; set; }

        [StringLength(100)]
        public string Search { get; set; }

        [StringLength(200)]
        public string Location { get; set; }

        [StringLength(10)]
        [RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
        public string Date { get; set; }

        [RegularExpression("^(overview|timeGridDay|timeGridWeek)$")]
        public string View { get; set; }
    }

    public class AvailabilityCalendarPageViewModel
    {
        public AvailabilityCalendarFilterViewModel Filter { get; set; } = new();
        public List<AvailabilityCalendarCategoryOption> Categories { get; set; } = new();
        public List<AvailabilityCalendarItemViewModel> Equipment { get; set; } = new();
        public List<string> Locations { get; set; } = new();
        public bool CanCreateReservations { get; set; }
        public string ValidationMessage { get; set; }
    }

    public class AvailabilityCalendarCategoryOption
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class AvailabilityCalendarItemViewModel
    {
        public long EquipmentId { get; set; }
        public long CategoryId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = string.Empty;
        public bool CanBeReserved { get; set; }
        public int BlockingCount { get; set; }
        public string AvailabilitySummary { get; set; } = string.Empty;
    }

    public class AvailabilityCalendarDataViewModel
    {
        public List<AvailabilityCalendarEventViewModel> Events { get; set; } = new();
        public List<AvailabilityCalendarItemViewModel> Items { get; set; } = new();
        public string ErrorMessage { get; set; }
    }

    public class AvailabilityCalendarEventViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public long EquipmentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
