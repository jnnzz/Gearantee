using ASI.Basecode.Services.ServiceModels.AvailabilityCalendar;
using System;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAvailabilityCalendarService
    {
        AvailabilityCalendarPageViewModel GetCalendarPage(AvailabilityCalendarFilterViewModel filter);
        AvailabilityCalendarDataViewModel GetCalendarData(
            AvailabilityCalendarFilterViewModel filter,
            DateTimeOffset start,
            DateTimeOffset end);
    }
}
