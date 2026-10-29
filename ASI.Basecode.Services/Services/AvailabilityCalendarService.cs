using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.AvailabilityCalendar;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class AvailabilityCalendarService : IAvailabilityCalendarService
    {
        public const int MaxItemsPerCalendar = 1000;

        private readonly IEquipmentItemRepository _equipmentItemRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly TimeProvider _timeProvider;

        public AvailabilityCalendarService(
            IEquipmentItemRepository equipmentItemRepository,
            IReservationRepository reservationRepository,
            TimeProvider timeProvider)
        {
            _equipmentItemRepository = equipmentItemRepository;
            _reservationRepository = reservationRepository;
            _timeProvider = timeProvider;
        }

        public AvailabilityCalendarPageViewModel GetCalendarPage(
            AvailabilityCalendarFilterViewModel filter)
        {
            filter ??= new AvailabilityCalendarFilterViewModel();
            NormalizeFilter(filter);

            var equipment = ActiveEquipment()
                .OrderBy(item => item.Category.CategoryName)
                .ThenBy(item => item.ItemCode)
                .Select(item => new AvailabilityCalendarItemViewModel
                {
                    EquipmentId = item.EquipmentId,
                    CategoryId = item.CategoryId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CategoryName = item.Category.CategoryName,
                    Location = item.Location,
                    OperationalStatus = item.ItemStatus,
                    CanBeReserved = item.ItemStatus == DomainValues.EquipmentStatuses.Available ||
                        item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed
                })
                .ToList();

            return new AvailabilityCalendarPageViewModel
            {
                Filter = filter,
                Equipment = equipment,
                Categories = equipment
                    .GroupBy(item => new { item.CategoryId, item.CategoryName })
                    .Select(group => new AvailabilityCalendarCategoryOption
                    {
                        CategoryId = group.Key.CategoryId,
                        CategoryName = group.Key.CategoryName
                    })
                    .OrderBy(category => category.CategoryName)
                    .ToList(),
                Locations = equipment
                    .Select(item => item.Location?.Trim())
                    .Where(location => !string.IsNullOrEmpty(location))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        public AvailabilityCalendarDataViewModel GetCalendarData(
            AvailabilityCalendarFilterViewModel filter,
            DateTimeOffset start,
            DateTimeOffset end)
        {
            filter ??= new AvailabilityCalendarFilterViewModel();
            NormalizeFilter(filter);

            var equipment = FilterEquipment(filter)
                .OrderBy(item => item.Category.CategoryName)
                .ThenBy(item => item.ItemCode)
                .Select(item => new AvailabilityCalendarItemViewModel
                {
                    EquipmentId = item.EquipmentId,
                    CategoryId = item.CategoryId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CategoryName = item.Category.CategoryName,
                    Location = item.Location,
                    OperationalStatus = item.ItemStatus,
                    CanBeReserved = item.ItemStatus == DomainValues.EquipmentStatuses.Available ||
                        item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed
                })
                .ToList();

            if (equipment.Count > MaxItemsPerCalendar)
            {
                return new AvailabilityCalendarDataViewModel
                {
                    ErrorMessage = "Too many equipment items match. Filter by category, item, or location and try again."
                };
            }

            var result = new AvailabilityCalendarDataViewModel { Items = equipment };
            if (equipment.Count == 0)
            {
                return result;
            }

            var startUtc = start.UtcDateTime;
            var endUtc = end.UtcDateTime;
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var equipmentIds = equipment.Select(item => item.EquipmentId).ToArray();
            var reservations = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => equipmentIds.Contains(reservation.EquipmentId))
                .Where(ReservationAvailability.Blocks(startUtc, endUtc, nowUtc))
                .Select(reservation => new
                {
                    reservation.ReservationId,
                    reservation.EquipmentId,
                    reservation.ReservationStart,
                    reservation.ReservationEnd,
                    ItemCode = reservation.EquipmentItem.ItemCode,
                    ItemName = reservation.EquipmentItem.ItemName,
                    Location = reservation.EquipmentItem.Location,
                    ActualReleaseAt = reservation.ReleaseRecord == null
                        ? (DateTime?)null
                        : reservation.ReleaseRecord.ActualReleaseAt,
                    IsReturned = reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord != null
                })
                .ToList();

            foreach (var reservation in reservations)
            {
                if (reservation.IsReturned)
                {
                    continue;
                }

                var isOverdue = reservation.ActualReleaseAt.HasValue &&
                    reservation.ReservationEnd <= nowUtc;
                var status = isOverdue
                    ? "Overdue"
                    : reservation.ActualReleaseAt.HasValue ? "Borrowed" : "Reserved";
                var eventStart = reservation.ActualReleaseAt ?? reservation.ReservationStart;
                var eventEnd = isOverdue ? endUtc : reservation.ReservationEnd;
                eventStart = Max(eventStart, startUtc);
                eventEnd = Min(eventEnd, endUtc);
                if (eventStart >= eventEnd)
                {
                    continue;
                }

                result.Events.Add(new AvailabilityCalendarEventViewModel
                {
                    Id = reservation.ReservationId.ToString(),
                    Title = $"{reservation.ItemCode} Â· {status}",
                    Start = AsUtcOffset(eventStart),
                    End = AsUtcOffset(eventEnd),
                    EquipmentId = reservation.EquipmentId,
                    Status = status,
                    Location = reservation.Location ?? string.Empty
                });
            }

            var blockingCounts = result.Events
                .GroupBy(calendarEvent => calendarEvent.EquipmentId)
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (var item in result.Items)
            {
                blockingCounts.TryGetValue(item.EquipmentId, out var count);
                item.BlockingCount = count;
                item.AvailabilitySummary = !item.CanBeReserved
                    ? item.OperationalStatus == DomainValues.EquipmentStatuses.UnderMaintenance
                        ? "Under maintenance Â· no return date recorded"
                        : "Unavailable for reservations"
                    : count == 0
                        ? "Available throughout this period"
                        : $"{count} scheduled block{(count == 1 ? string.Empty : "s")} in this period";
            }

            return result;
        }

        private IQueryable<EquipmentItem> ActiveEquipment()
        {
            return _equipmentItemRepository.GetEquipmentItems()
                .AsNoTracking()
                .Where(item => !item.IsArchived &&
                    item.ItemStatus != DomainValues.EquipmentStatuses.Archived &&
                    item.Category.IsActive);
        }

        private IQueryable<EquipmentItem> FilterEquipment(
            AvailabilityCalendarFilterViewModel filter)
        {
            var query = ActiveEquipment();
            if (filter.CategoryId.HasValue)
            {
                query = query.Where(item => item.CategoryId == filter.CategoryId.Value);
            }

            if (filter.EquipmentId.HasValue)
            {
                query = query.Where(item => item.EquipmentId == filter.EquipmentId.Value);
            }

            if (!string.IsNullOrEmpty(filter.Location))
            {
                query = query.Where(item => item.Location == filter.Location);
            }

            return query;
        }

        private static void NormalizeFilter(AvailabilityCalendarFilterViewModel filter)
        {
            filter.Location = filter.Location?.Trim();
        }

        private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;
        private static DateTime Min(DateTime left, DateTime right) => left < right ? left : right;

        private static DateTimeOffset AsUtcOffset(DateTime value) =>
            new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
