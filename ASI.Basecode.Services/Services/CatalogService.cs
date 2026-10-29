using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Catalog;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class CatalogService : ICatalogService
    {
        public const int PageSize = PaginationDefaults.PageSize;
        private const int MaxBookedWindows = 20;

        private readonly IEquipmentItemRepository _equipmentItemRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly TimeProvider _timeProvider;

        public CatalogService(
            IEquipmentItemRepository equipmentItemRepository,
            IReservationRepository reservationRepository,
            TimeProvider timeProvider)
        {
            _equipmentItemRepository = equipmentItemRepository;
            _reservationRepository = reservationRepository;
            _timeProvider = timeProvider;
        }

        public CatalogIndexViewModel GetCatalog(CatalogFilterViewModel filter)
        {
            filter ??= new CatalogFilterViewModel();
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var model = new CatalogIndexViewModel { Filter = filter };

            var reservable = _equipmentItemRepository.GetEquipmentItems()
                .AsNoTracking()
                .Where(ReservationAvailability.IsReservable());

            model.Categories = reservable
                .Select(item => new CatalogCategoryOption
                {
                    CategoryId = item.CategoryId,
                    CategoryName = item.Category.CategoryName
                })
                .Distinct()
                .OrderBy(option => option.CategoryName)
                .ToList();

            var items = reservable;

            var search = filter.Search?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                items = items.Where(item =>
                    item.ItemName.Contains(search) ||
                    item.ItemCode.Contains(search) ||
                    (item.Brand != null && item.Brand.Contains(search)) ||
                    (item.Model != null && item.Model.Contains(search)));
            }

            if (filter.CategoryId.HasValue)
            {
                items = items.Where(item => item.CategoryId == filter.CategoryId.Value);
            }

            if (filter.From.HasValue || filter.To.HasValue)
            {
                model.WindowError = ReservationAvailability.ValidateWindow(filter.From, filter.To, nowUtc);
                if (model.WindowError == null)
                {
                    var startUtc = ManilaClock.ToUtc(filter.From.Value);
                    var endUtc = ManilaClock.ToUtc(filter.To.Value);
                    var blocking = _reservationRepository.GetReservations()
                        .Where(ReservationAvailability.Blocks(startUtc, endUtc, nowUtc));
                    items = items.Where(item =>
                        !blocking.Any(reservation => reservation.EquipmentId == item.EquipmentId));
                }
            }

            model.TotalCount = items.Count();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)PageSize));
            model.Page = Math.Clamp(filter.Page, 1, model.TotalPages);

            var activeLoans = ActiveLoans();
            model.Items = items
                .OrderBy(item => item.ItemName)
                .ThenBy(item => item.ItemCode)
                .Skip((model.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(item => new CatalogItemViewModel
                {
                    EquipmentId = item.EquipmentId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CategoryName = item.Category.CategoryName,
                    Brand = item.Brand,
                    Model = item.Model,
                    Location = item.Location,
                    ConditionStatus = item.ConditionStatus,
                    ImageUrl = item.ImageUrl,
                    AvailabilityStatus = activeLoans.Any(loan => loan.EquipmentId == item.EquipmentId)
                        ? DomainValues.EquipmentStatuses.Borrowed
                        : DomainValues.EquipmentStatuses.Available
                })
                .ToList();

            return model;
        }

        public CatalogItemDetailsViewModel RetrieveCatalogItem(long id)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var activeLoans = ActiveLoans();

            var item = _equipmentItemRepository.GetEquipmentItems()
                .AsNoTracking()
                .Where(ReservationAvailability.IsReservable())
                .Where(x => x.EquipmentId == id)
                .Select(s => new CatalogItemDetailsViewModel
                {
                    EquipmentId = s.EquipmentId,
                    ItemCode = s.ItemCode,
                    ItemName = s.ItemName,
                    CategoryName = s.Category.CategoryName,
                    Description = s.Description,
                    Brand = s.Brand,
                    Model = s.Model,
                    SerialNumber = s.SerialNumber,
                    Location = s.Location,
                    ConditionStatus = s.ConditionStatus,
                    ImageUrl = s.ImageUrl,
                    AvailabilityStatus = activeLoans.Any(loan => loan.EquipmentId == s.EquipmentId)
                        ? DomainValues.EquipmentStatuses.Borrowed
                        : DomainValues.EquipmentStatuses.Available
                })
                .FirstOrDefault();

            if (item == null)
            {
                return null;
            }

            // Everything that blocks a window from now onward, including overdue loans.
            var windows = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.EquipmentId == id)
                .Where(ReservationAvailability.Blocks(nowUtc, DateTime.MaxValue, nowUtc))
                .OrderBy(reservation => reservation.ReservationStart)
                .Take(MaxBookedWindows)
                .Select(reservation => new
                {
                    reservation.ReservationStart,
                    reservation.ReservationEnd,
                    IsOnLoan = reservation.ReleaseRecord != null
                })
                .ToList();

            item.BookedWindows = windows
                .Select(window => new BookedWindowViewModel
                {
                    StartLocal = ManilaClock.ToLocal(window.ReservationStart),
                    EndLocal = ManilaClock.ToLocal(window.ReservationEnd),
                    Label = window.IsOnLoan ? "On loan" : "Reserved"
                })
                .ToList();

            return item;
        }

        private IQueryable<Reservation> ActiveLoans()
        {
            return _reservationRepository.GetReservations()
                .Where(reservation =>
                    reservation.ReleaseRecord != null &&
                    reservation.ReleaseRecord.ReturnRecord == null);
        }
    }
}
