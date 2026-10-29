using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class BorrowingHistoryService : IBorrowingHistoryService
    {
        public const int PageSize = PaginationDefaults.PageSize;
        public const int MaxFilterLength = 100;

        private static readonly string[] Statuses =
        {
            DomainValues.ReservationStatuses.Pending,
            DomainValues.ReservationStatuses.Approved,
            "Borrowed",
            "Overdue",
            "Returned",
            DomainValues.ReservationStatuses.Rejected,
            DomainValues.ReservationStatuses.Cancelled,
            DomainValues.ReservationStatuses.Expired
        };

        private readonly AsiBasecodeDBContext _db;
        private readonly TimeProvider _timeProvider;

        public BorrowingHistoryService(
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _db = unitOfWork.Context;
            _timeProvider = timeProvider;
        }

        public async Task<BorrowingHistoryIndexViewModel> GetHistoryAsync(
            BorrowingHistoryFilterViewModel filter,
            string userId,
            bool onlyOwnHistory,
            CancellationToken cancellationToken = default)
        {
            filter ??= new BorrowingHistoryFilterViewModel();
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var model = new BorrowingHistoryIndexViewModel
            {
                Filter = filter,
                StatusOptions = Statuses.ToList()
            };

            NormalizeFilter(filter);
            await LoadFilterOptionsAsync(model, userId, onlyOwnHistory, cancellationToken);

            model.ValidationMessage = ValidateFilter(filter);
            if (model.ValidationMessage != null)
            {
                return model;
            }

            var query = ApplyFilters(
                ScopedReservations(userId, onlyOwnHistory),
                filter,
                nowUtc);

            model.TotalCount = await query.CountAsync(cancellationToken);
            model.TotalPages = Math.Max(
                1,
                (int)Math.Ceiling(model.TotalCount / (double)PageSize));
            model.Page = Math.Clamp(filter.Page, 1, model.TotalPages);
            filter.Page = model.Page;

            var rows = await query
                .OrderByDescending(reservation => reservation.ReservationStart)
                .ThenByDescending(reservation => reservation.ReservationId)
                .Skip((model.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(HistoryProjection)
                .ToListAsync(cancellationToken);

            model.Items = rows
                .Select(row => MapRow(row, nowUtc))
                .ToList();
            return model;
        }

        public async IAsyncEnumerable<BorrowingHistoryRowViewModel> StreamReportRowsAsync(
            BorrowingHistoryFilterViewModel filter,
            string userId,
            bool onlyOwnHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            filter ??= new BorrowingHistoryFilterViewModel();
            NormalizeFilter(filter);
            if (ValidateFilter(filter) != null)
            {
                yield break;
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var query = ApplyFilters(
                ScopedReservations(userId, onlyOwnHistory),
                filter,
                nowUtc);

            await foreach (var row in query
                .OrderByDescending(reservation => reservation.ReservationStart)
                .ThenByDescending(reservation => reservation.ReservationId)
                .Select(HistoryProjection)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken))
            {
                yield return MapRow(row, nowUtc);
            }
        }

        public static string ValidateFilter(BorrowingHistoryFilterViewModel filter)
        {
            if (filter == null)
            {
                return null;
            }

            if (filter.Search?.Length > MaxFilterLength ||
                filter.Borrower?.Length > MaxFilterLength ||
                filter.Item?.Length > MaxFilterLength)
            {
                return $"Search fields must be {MaxFilterLength} characters or fewer.";
            }

            if (filter.CategoryId <= 0)
            {
                return "Choose a valid category.";
            }

            if (filter.FromDate.HasValue && filter.FromDate.Value.Year < 2 ||
                filter.ToDate.HasValue && filter.ToDate.Value.Year < 2 ||
                filter.ToDate.HasValue && filter.ToDate.Value.Date >= DateTime.MaxValue.Date)
            {
                return "Enter a valid date range.";
            }

            if (filter.FromDate.HasValue && filter.ToDate.HasValue &&
                filter.FromDate.Value.Date > filter.ToDate.Value.Date)
            {
                return "The start date must be on or before the end date.";
            }

            if (!string.IsNullOrEmpty(filter.Status) &&
                !Statuses.Contains(filter.Status, StringComparer.Ordinal))
            {
                return "Choose a valid history status.";
            }

            if (filter.LateReturn != "" &&
                filter.LateReturn != "yes" &&
                filter.LateReturn != "no")
            {
                return "Choose a valid late-return filter.";
            }

            return null;
        }

        public static string NormalizeAndValidateFilter(
            BorrowingHistoryFilterViewModel filter)
        {
            if (filter == null)
            {
                return null;
            }

            NormalizeFilter(filter);
            return ValidateFilter(filter);
        }

        private IQueryable<Reservation> ScopedReservations(
            string userId,
            bool onlyOwnHistory)
        {
            var query = _db.Reservations.AsNoTracking();
            if (onlyOwnHistory)
            {
                query = query.Where(reservation =>
                    reservation.BorrowerProfile.UserId == userId);
            }

            return query;
        }

        private async Task LoadFilterOptionsAsync(
            BorrowingHistoryIndexViewModel model,
            string userId,
            bool onlyOwnHistory,
            CancellationToken cancellationToken)
        {
            var scopedReservations = ScopedReservations(userId, onlyOwnHistory);
            var categoryIds = scopedReservations
                .Select(reservation => reservation.EquipmentItem.CategoryId)
                .Distinct();

            model.Categories = await _db.EquipmentCategories
                .AsNoTracking()
                .Where(category => categoryIds.Contains(category.CategoryId))
                .OrderBy(category => category.CategoryName)
                .Select(category => new BorrowingHistoryCategoryOption
                {
                    CategoryId = category.CategoryId,
                    CategoryName = category.CategoryName
                })
                .ToListAsync(cancellationToken);

        }

        private static IQueryable<Reservation> ApplyFilters(
            IQueryable<Reservation> query,
            BorrowingHistoryFilterViewModel filter,
            DateTime nowUtc)
        {
            if (filter.CategoryId.HasValue)
            {
                query = query.Where(reservation =>
                    reservation.EquipmentItem.CategoryId == filter.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search;
                query = query.Where(reservation =>
                    ((reservation.BorrowerProfile.User.FirstName ?? "") + " " +
                        (reservation.BorrowerProfile.User.LastName ?? "")).Contains(search) ||
                    (reservation.BorrowerProfile.User.UserCode != null &&
                        reservation.BorrowerProfile.User.UserCode.Contains(search)) ||
                    reservation.EquipmentItem.ItemName.Contains(search) ||
                    reservation.EquipmentItem.ItemCode.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(filter.Borrower))
            {
                var borrower = filter.Borrower;
                query = query.Where(reservation =>
                    ((reservation.BorrowerProfile.User.FirstName ?? "") + " " +
                        (reservation.BorrowerProfile.User.LastName ?? "")).Contains(borrower) ||
                    (reservation.BorrowerProfile.User.UserCode != null &&
                        reservation.BorrowerProfile.User.UserCode.Contains(borrower)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Item))
            {
                var item = filter.Item;
                query = query.Where(reservation =>
                    reservation.EquipmentItem.ItemName.Contains(item) ||
                    reservation.EquipmentItem.ItemCode.Contains(item));
            }

            if (filter.FromDate.HasValue)
            {
                var fromUtc = ManilaClock.ToUtc(filter.FromDate.Value.Date);
                query = query.Where(reservation =>
                    reservation.ReservationStart >= fromUtc);
            }

            if (filter.ToDate.HasValue)
            {
                var toUtc = ManilaClock.ToUtc(filter.ToDate.Value.Date.AddDays(1));
                query = query.Where(reservation =>
                    reservation.ReservationStart < toUtc);
            }

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = filter.Status switch
                {
                    "Borrowed" => query.Where(reservation =>
                        reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord == null &&
                        reservation.ReservationEnd >= nowUtc),
                    "Overdue" => query.Where(reservation =>
                        reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord == null &&
                        reservation.ReservationEnd < nowUtc),
                    "Returned" => query.Where(reservation =>
                        reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord != null),
                    DomainValues.ReservationStatuses.Approved => query.Where(reservation =>
                        reservation.Status == filter.Status &&
                        reservation.ReleaseRecord == null),
                    _ => query.Where(reservation =>
                        reservation.Status == filter.Status &&
                        reservation.ReleaseRecord == null)
                };
            }

            if (filter.LateReturn == "yes")
            {
                query = query.Where(reservation =>
                    reservation.ReleaseRecord != null &&
                    reservation.ReleaseRecord.ReturnRecord != null &&
                    (reservation.ReleaseRecord.ReturnRecord.LateReturn != null ||
                     reservation.ReleaseRecord.ReturnRecord.ActualReturnAt >
                        reservation.ReservationEnd));
            }
            else if (filter.LateReturn == "no")
            {
                query = query.Where(reservation =>
                    reservation.ReleaseRecord == null ||
                    reservation.ReleaseRecord.ReturnRecord == null ||
                    (reservation.ReleaseRecord.ReturnRecord.LateReturn == null &&
                     reservation.ReleaseRecord.ReturnRecord.ActualReturnAt <=
                        reservation.ReservationEnd));
            }

            return query;
        }

        private static BorrowingHistoryRowViewModel MapRow(
            HistoryProjectionRow row,
            DateTime nowUtc)
        {
            var isReturned = row.ReturnedAtUtc.HasValue;
            var isLate = isReturned &&
                (row.HasLateReturnRecord || row.ReturnedAtUtc.Value > row.DueUtc);
            var status = isReturned
                ? "Returned"
                : row.ReleaseRecordId.HasValue
                    ? row.DueUtc < nowUtc ? "Overdue" : "Borrowed"
                    : row.ReservationStatus;
            var daysLate = 0;
            if (isLate)
            {
                daysLate = Math.Max(
                    1,
                    row.StoredDaysLate ?? (int)Math.Ceiling(
                        (row.ReturnedAtUtc.Value - row.DueUtc).TotalDays));
            }

            return new BorrowingHistoryRowViewModel
            {
                ReservationId = row.ReservationId,
                LoanCode = row.ReleaseRecordId.HasValue
                    ? $"LN-{row.ReleaseRecordId.Value}"
                    : null,
                BorrowerName = row.BorrowerName,
                UserCode = row.UserCode,
                SchoolId = row.SchoolId,
                ItemCode = row.ItemCode,
                ItemName = row.ItemName,
                CategoryName = row.CategoryName,
                Status = status,
                RequestedAtLocal = ManilaClock.ToLocal(row.RequestedAtUtc),
                StartLocal = ManilaClock.ToLocal(row.StartUtc),
                DueLocal = ManilaClock.ToLocal(row.DueUtc),
                ReleasedAtLocal = row.ReleasedAtUtc.HasValue
                    ? ManilaClock.ToLocal(row.ReleasedAtUtc.Value)
                    : null,
                ReturnedAtLocal = row.ReturnedAtUtc.HasValue
                    ? ManilaClock.ToLocal(row.ReturnedAtUtc.Value)
                    : null,
                ReturnedCondition = row.ReturnedCondition,
                HasLateReturn = isLate,
                DaysLate = daysLate
            };
        }

        private static void NormalizeFilter(BorrowingHistoryFilterViewModel filter)
        {
            filter.Search = filter.Search?.Trim() ?? string.Empty;
            filter.Borrower = filter.Borrower?.Trim() ?? string.Empty;
            filter.Item = filter.Item?.Trim() ?? string.Empty;
            filter.Status = filter.Status?.Trim() ?? string.Empty;
            filter.LateReturn = filter.LateReturn?.Trim().ToLowerInvariant() ?? string.Empty;
            filter.Page = Math.Max(1, filter.Page);

            if (!string.IsNullOrEmpty(filter.Status))
            {
                filter.Status = Statuses.FirstOrDefault(status =>
                    string.Equals(status, filter.Status, StringComparison.OrdinalIgnoreCase))
                    ?? filter.Status;
            }
        }

        private static readonly System.Linq.Expressions.Expression<
            Func<Reservation, HistoryProjectionRow>> HistoryProjection = reservation =>
                new HistoryProjectionRow
                {
                    ReservationId = reservation.ReservationId,
                    BorrowerName = ((reservation.BorrowerProfile.User.FirstName ?? "") + " " +
                        (reservation.BorrowerProfile.User.LastName ?? "")).Trim(),
                    UserCode = reservation.BorrowerProfile.User.UserCode,
                    SchoolId = reservation.BorrowerProfile.User.UserCode,
                    ItemCode = reservation.EquipmentItem.ItemCode,
                    ItemName = reservation.EquipmentItem.ItemName,
                    CategoryName = reservation.EquipmentItem.Category.CategoryName,
                    ReservationStatus = reservation.Status,
                    RequestedAtUtc = reservation.RequestedAt,
                    StartUtc = reservation.ReservationStart,
                    DueUtc = reservation.ReservationEnd,
                    ReleaseRecordId = reservation.ReleaseRecord == null
                        ? (long?)null
                        : reservation.ReleaseRecord.ReleaseRecordId,
                    ReleasedAtUtc = reservation.ReleaseRecord == null
                        ? (DateTime?)null
                        : reservation.ReleaseRecord.ActualReleaseAt,
                    ReturnedAtUtc = reservation.ReleaseRecord == null ||
                        reservation.ReleaseRecord.ReturnRecord == null
                        ? (DateTime?)null
                        : reservation.ReleaseRecord.ReturnRecord.ActualReturnAt,
                    ReturnedCondition = reservation.ReleaseRecord == null ||
                        reservation.ReleaseRecord.ReturnRecord == null
                        ? null
                        : reservation.ReleaseRecord.ReturnRecord.ReturnedCondition,
                    HasLateReturnRecord = reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord.LateReturn != null,
                    StoredDaysLate = reservation.ReleaseRecord == null ||
                        reservation.ReleaseRecord.ReturnRecord == null ||
                        reservation.ReleaseRecord.ReturnRecord.LateReturn == null
                        ? (int?)null
                        : reservation.ReleaseRecord.ReturnRecord.LateReturn.DaysLate
                };

        private sealed class HistoryProjectionRow
        {
            public long ReservationId { get; set; }
            public string BorrowerName { get; set; }
            public string UserCode { get; set; }
            public string SchoolId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string CategoryName { get; set; }
            public string ReservationStatus { get; set; }
            public DateTime RequestedAtUtc { get; set; }
            public DateTime StartUtc { get; set; }
            public DateTime DueUtc { get; set; }
            public long? ReleaseRecordId { get; set; }
            public DateTime? ReleasedAtUtc { get; set; }
            public DateTime? ReturnedAtUtc { get; set; }
            public string ReturnedCondition { get; set; }
            public bool HasLateReturnRecord { get; set; }
            public int? StoredDaysLate { get; set; }
        }
    }
}
