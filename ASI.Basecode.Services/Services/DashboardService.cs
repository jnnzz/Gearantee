using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Dashboard;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class DashboardService : ServiceBase, IDashboardService
    {
        private static readonly Expression<Func<Reservation, ReservationRow>>
            ReservationProjection = reservation => new ReservationRow
            {
                ReservationId = reservation.ReservationId,
                ItemCode = reservation.EquipmentItem.ItemCode,
                ItemName = reservation.EquipmentItem.ItemName,
                BorrowerName = (reservation.BorrowerProfile.User.FirstName + " " +
                    reservation.BorrowerProfile.User.LastName).Trim(),
                SchoolId = reservation.BorrowerProfile.User.UserCode,
                Department = reservation.BorrowerProfile.Department,
                ContactNumber = reservation.BorrowerProfile.ContactNumber,
                StartUtc = reservation.ReservationStart,
                EndUtc = reservation.ReservationEnd,
                RequestedAtUtc = reservation.RequestedAt,
                Status = reservation.Status,
                RejectionReason = reservation.RejectionReason,
                Purpose = reservation.Purpose,
                ReviewerName = reservation.ReviewedByUser == null
                    ? null
                    : (reservation.ReviewedByUser.FirstName + " " +
                        reservation.ReviewedByUser.LastName).Trim(),
                CategoryName = reservation.EquipmentItem.Category.CategoryName,
                LocationName = reservation.EquipmentItem.Location,
                ImageUrl = reservation.EquipmentItem.ImageUrl,
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
                    : reservation.ReleaseRecord.ReturnRecord.ReturnedCondition
            };

        private readonly AsiBasecodeDBContext _db;
        private readonly TimeProvider _timeProvider;

        public DashboardService(
            IUnitOfWork unitOfWork,
            ILoggerFactory loggerFactory,
            TimeProvider timeProvider)
            : base(loggerFactory)
        {
            _db = unitOfWork.Context;
            _timeProvider = timeProvider;
        }

        public async Task<BorrowerDashboardModel> GetBorrowerDashboardAsync(
            string userId)
        {
            var displayName = await _db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (user.FirstName + " " + user.LastName).Trim())
                .FirstOrDefaultAsync();

            var model = new BorrowerDashboardModel
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? "there"
                    : displayName.Split(' ')[0]
            };

            var profile = await _db.BorrowerProfiles
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new
                {
                    item.BorrowerProfileId,
                    item.IsEligible
                })
                .FirstOrDefaultAsync();

            if (profile == null)
            {
                return model;
            }

            model.HasProfile = true;
            model.IsEligible = profile.IsEligible;
            var profileId = profile.BorrowerProfileId;
            var mine = _db.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.BorrowerProfileId == profileId);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var (todayStart, _) = ManilaClock.TodayUtcRange(now);

            model.PendingCount = await mine.CountAsync(reservation =>
                reservation.Status == DomainValues.ReservationStatuses.Pending);
            model.ApprovedAwaitingPickupCount = await mine.CountAsync(reservation =>
                reservation.Status == DomainValues.ReservationStatuses.Approved &&
                reservation.ReservationStart >= todayStart &&
                reservation.ReleaseRecord == null);
            model.ActiveLoanCount = await mine.CountAsync(reservation =>
                reservation.ReleaseRecord != null &&
                reservation.ReleaseRecord.ReturnRecord == null);
            model.OverdueCount = await mine.CountAsync(reservation =>
                reservation.ReleaseRecord != null &&
                reservation.ReleaseRecord.ReturnRecord == null &&
                reservation.ReservationEnd < now);
            model.AvailableItemCount = await _db.EquipmentItems
                .AsNoTracking()
                .CountAsync(item => !item.IsArchived &&
                    item.ItemStatus == DomainValues.EquipmentStatuses.Available);
            model.AvailableCategoryCount = await _db.EquipmentItems
                .AsNoTracking()
                .Where(item => !item.IsArchived &&
                    item.ItemStatus == DomainValues.EquipmentStatuses.Available &&
                    item.Category.IsActive)
                .Select(item => item.CategoryId)
                .Distinct()
                .CountAsync();
            model.LateReturnCount = await _db.LateReturns
                .AsNoTracking()
                .CountAsync(item =>
                    item.ReturnRecord.ReleaseRecord.Reservation.BorrowerProfileId == profileId);

            var rows = mine.Select(ReservationProjection);
            var currentLoans = await rows
                .Where(reservation => reservation.ReleaseRecordId.HasValue &&
                    reservation.ReturnedAtUtc == null)
                .OrderBy(reservation => reservation.EndUtc >= now)
                .ThenBy(reservation => reservation.EndUtc)
                .Take(10)
                .ToListAsync();
            model.CurrentLoans = DecorateRows(currentLoans, now);

            var upcomingPickups = await rows
                .Where(reservation =>
                    reservation.Status == DomainValues.ReservationStatuses.Approved &&
                    !reservation.ReleaseRecordId.HasValue &&
                    reservation.StartUtc >= todayStart)
                .OrderBy(reservation => reservation.StartUtc)
                .Take(5)
                .ToListAsync();
            model.UpcomingPickups = DecorateRows(upcomingPickups, now);

            var recentRequests = await rows
                .OrderByDescending(reservation => reservation.RequestedAtUtc)
                .Take(5)
                .ToListAsync();
            model.RecentRequests = DecorateRows(recentRequests, now);
            return model;
        }

        public async Task<CustodianDashboardModel> GetCustodianDashboardAsync()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var (todayStart, todayEnd) = ManilaClock.TodayUtcRange(now);
            var model = new CustodianDashboardModel();

            var pendingQuery = _db.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Status == DomainValues.ReservationStatuses.Pending &&
                    reservation.ReservationEnd > now);
            model.PendingCount = await pendingQuery.CountAsync();
            var within24Hours = now.AddHours(24);
            model.PendingStartingSoonCount = await pendingQuery.CountAsync(reservation =>
                reservation.ReservationStart >= now &&
                reservation.ReservationStart < within24Hours);
            var pending = await pendingQuery
                .OrderBy(reservation => reservation.RequestedAt)
                .Take(10)
                .Select(ReservationProjection)
                .ToListAsync();
            model.PendingQueue = DecorateRows(pending, now);

            var pickupQuery = _db.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Status == DomainValues.ReservationStatuses.Approved &&
                    reservation.ReleaseRecord == null &&
                    reservation.ReservationStart >= todayStart &&
                    reservation.ReservationStart < todayEnd);
            model.PickupsTodayCount = await pickupQuery.CountAsync();
            var pickups = await pickupQuery
                .OrderBy(reservation => reservation.ReservationStart)
                .Take(10)
                .Select(ReservationProjection)
                .ToListAsync();
            model.PickupsToday = DecorateRows(pickups, now);

            var activeLoans = ActiveLoans();
            var dueTodayQuery = activeLoans.Where(reservation =>
                reservation.ReservationEnd >= now &&
                reservation.ReservationEnd < todayEnd);
            model.ReturnsDueTodayCount = await dueTodayQuery.CountAsync();
            var dueToday = await dueTodayQuery
                .OrderBy(reservation => reservation.ReservationEnd)
                .Take(10)
                .Select(ReservationProjection)
                .ToListAsync();
            model.ReturnsDueToday = DecorateRows(dueToday, now);

            var overdueQuery = ActiveLoans()
                .Where(reservation => reservation.ReservationEnd < now);
            model.OverdueCount = await overdueQuery.CountAsync();
            var overdue = await overdueQuery
                .OrderBy(reservation => reservation.ReservationEnd)
                .Take(10)
                .Select(ReservationProjection)
                .ToListAsync();
            model.Overdue = DecorateRows(overdue, now);

            model.NeedsAttention = await _db.EquipmentItems
                .AsNoTracking()
                .Where(item => !item.IsArchived &&
                    (item.ItemStatus == DomainValues.EquipmentStatuses.UnderMaintenance ||
                     item.ItemStatus == DomainValues.EquipmentStatuses.Unavailable))
                .OrderBy(item => item.ItemName)
                .Take(5)
                .Select(item => new ItemRow
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    ItemStatus = item.ItemStatus,
                    ConditionStatus = item.ConditionStatus,
                    LocationName = item.Location
                })
                .ToListAsync();

            model.Desk = model.Overdue
                .Select(row => new DashboardTask { Kind = "RETURN", Row = row, SortKey = 0 })
                .Concat(model.ReturnsDueToday.Select(row =>
                    new DashboardTask { Kind = "RETURN", Row = row, SortKey = 1 }))
                .Concat(model.PickupsToday.Select(row =>
                    new DashboardTask { Kind = "RELEASE", Row = row, SortKey = 2 }))
                .Concat(model.PendingQueue.Select(row =>
                    new DashboardTask { Kind = "APPROVE", Row = row, SortKey = 3 }))
                .OrderBy(task => task.SortKey)
                .ThenBy(task => task.SortKey == 0 || task.SortKey == 1
                    ? task.Row.EndUtc
                    : task.SortKey == 2
                        ? task.Row.StartUtc
                        : task.Row.RequestedAtUtc)
                .ToList();

            return model;
        }

        public async Task<AdministratorDashboardModel>
            GetAdministratorDashboardAsync()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var (todayStart, todayEnd) = ManilaClock.TodayUtcRange(now);
            var model = new AdministratorDashboardModel();

            model.TotalItems = await _db.EquipmentItems
                .AsNoTracking()
                .CountAsync(item => !item.IsArchived);
            model.ActiveCategories = await _db.EquipmentCategories
                .AsNoTracking()
                .CountAsync(category => category.IsActive);
            model.ActiveLoanCount = await ActiveLoans().CountAsync();
            model.OverdueLoanCount = await ActiveLoans()
                .CountAsync(reservation => reservation.ReservationEnd < now);
            model.UnderMaintenanceCount = await _db.EquipmentItems
                .AsNoTracking()
                .CountAsync(item => !item.IsArchived &&
                    item.ItemStatus == DomainValues.EquipmentStatuses.UnderMaintenance);
            model.DamagedAwaitingReviewCount = await _db.ReturnRecords
                .AsNoTracking()
                .CountAsync(record =>
                    record.ReturnedCondition == DomainValues.ReturnConditions.Damaged &&
                    record.ReleaseRecord.Reservation.EquipmentItem.ItemStatus ==
                        DomainValues.EquipmentStatuses.UnderMaintenance);
            model.IneligibleBorrowerCount = await _db.BorrowerProfiles
                .AsNoTracking()
                .CountAsync(profile => !profile.IsEligible && profile.User.IsActive);

            var categories = await _db.EquipmentCategories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.CategoryName)
                .Select(category => new
                {
                    category.CategoryId,
                    category.CategoryName
                })
                .ToListAsync();

            var inventoryCounts = await _db.EquipmentItems
                .AsNoTracking()
                .Where(item => !item.IsArchived && item.Category.IsActive)
                .GroupBy(item => new
                {
                    item.CategoryId,
                    item.Category.CategoryName
                })
                .Select(group => new
                {
                    group.Key.CategoryId,
                    group.Key.CategoryName,
                    Available = group.Count(item =>
                        item.ItemStatus == DomainValues.EquipmentStatuses.Available),
                    Borrowed = group.Count(item =>
                        item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed),
                    Maintenance = group.Count(item =>
                        item.ItemStatus == DomainValues.EquipmentStatuses.UnderMaintenance),
                    Unavailable = group.Count(item =>
                        item.ItemStatus == DomainValues.EquipmentStatuses.Unavailable)
                })
                .ToListAsync();

            var reservedCounts = await _db.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Status == DomainValues.ReservationStatuses.Approved &&
                    reservation.ReleaseRecord == null &&
                    reservation.ReservationStart < todayEnd &&
                    reservation.ReservationEnd > todayStart &&
                    !reservation.EquipmentItem.IsArchived &&
                    reservation.EquipmentItem.ItemStatus ==
                        DomainValues.EquipmentStatuses.Available &&
                    reservation.EquipmentItem.Category.IsActive)
                .GroupBy(reservation => reservation.EquipmentItem.CategoryId)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    Count = group.Select(item => item.EquipmentId).Distinct().Count()
                })
                .ToDictionaryAsync(item => item.CategoryId, item => item.Count);

            var countsByCategory = inventoryCounts.ToDictionary(
                item => item.CategoryId,
                item => item);
            foreach (var category in categories)
            {
                countsByCategory.TryGetValue(category.CategoryId, out var counts);
                reservedCounts.TryGetValue(category.CategoryId, out var reserved);
                var available = counts?.Available ?? 0;
                reserved = Math.Min(reserved, available);
                model.InventoryByCategory.Add(new CategoryInventoryRow
                {
                    CategoryId = category.CategoryId,
                    Name = category.CategoryName,
                    Available = available - reserved,
                    Reserved = reserved,
                    Borrowed = counts?.Borrowed ?? 0,
                    Maintenance = counts?.Maintenance ?? 0,
                    Unavailable = counts?.Unavailable ?? 0
                });
            }

            var damaged = new List<AttentionItem>();
            if (model.DamagedAwaitingReviewCount > 0)
            {
                damaged.Add(new AttentionItem
                {
                    Title = "Damaged items need inspection",
                    Detail = $"{model.DamagedAwaitingReviewCount} returned item(s) are still under maintenance.",
                    Href = "/Returns/Index",
                    IsAvailable = false
                });
            }

            var overdueRows = await ActiveLoans()
                .Where(reservation => reservation.ReservationEnd < now)
                .OrderBy(reservation => reservation.ReservationEnd)
                .Take(5)
                .Select(ReservationProjection)
                .ToListAsync();
            var overdueItems = DecorateRows(overdueRows, now)
                .Select(overdue => new AttentionItem
                {
                    Title = $"Overdue loan {overdue.LoanCode}",
                    Detail = $"{overdue.BorrowerName} Â· {overdue.ItemName} Â· {overdue.DaysOverdue} day(s) late.",
                    Href = "/Returns/Index",
                    IsAvailable = false
                })
                .ToList();

            var ineligible = new List<AttentionItem>();
            if (model.IneligibleBorrowerCount > 0)
            {
                ineligible.Add(new AttentionItem
                {
                    Title = "Borrowers need eligibility review",
                    Detail = $"{model.IneligibleBorrowerCount} active borrower profile(s) are ineligible.",
                    Href = "/BorrowerProfiles/Index?eligible=false",
                    IsAvailable = false
                });
            }

            var noSpare = model.InventoryByCategory
                .Where(item => item.Total > 0 && item.AvailableSpareCount == 0)
                .Take(5)
                .Select(category => new AttentionItem
                {
                    Title = $"No spare items in {category.Name}",
                    Detail = "No unreserved, available items are currently in this category.",
                    Href = "/EquipmentItems/Index",
                    IsAvailable = false
                })
                .ToList();

            model.NeedsAttention = MergeAttention(
                5,
                damaged,
                overdueItems,
                ineligible,
                noSpare);
            return model;
        }

        private static List<AttentionItem> MergeAttention(
            int limit,
            params List<AttentionItem>[] groups)
        {
            var result = groups
                .Where(group => group.Count > 0)
                .Select(group => group[0])
                .Take(limit)
                .ToList();

            foreach (var item in groups.SelectMany(group => group.Skip(1)))
            {
                if (result.Count >= limit)
                {
                    break;
                }

                result.Add(item);
            }

            return result;
        }

        private IQueryable<Reservation> ActiveLoans()
        {
            return _db.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.ReleaseRecord != null &&
                    reservation.ReleaseRecord.ReturnRecord == null);
        }

        private static List<ReservationRow> DecorateRows(
            List<ReservationRow> rows,
            DateTime nowUtc)
        {
            foreach (var row in rows)
            {
                row.DisplayStatus = GetDisplayStatus(row, nowUtc);
                if (row.ReleaseRecordId.HasValue &&
                    !row.ReturnedAtUtc.HasValue &&
                    row.EndUtc < nowUtc)
                {
                    row.DaysOverdue = (int)Math.Ceiling(
                        (nowUtc - row.EndUtc).TotalDays);
                }
            }

            return rows;
        }

        private static string GetDisplayStatus(
            ReservationRow row,
            DateTime nowUtc)
        {
            if (row.ReturnedAtUtc.HasValue)
            {
                return "Returned";
            }

            if (row.ReleaseRecordId.HasValue)
            {
                var (todayStart, todayEnd) = ManilaClock.TodayUtcRange(nowUtc);
                if (row.EndUtc < nowUtc)
                {
                    return "Overdue";
                }

                return row.EndUtc >= todayStart && row.EndUtc < todayEnd
                    ? "Due today"
                    : "Borrowed";
            }

            if (row.Status == DomainValues.ReservationStatuses.Approved)
            {
                if (row.EndUtc < nowUtc)
                {
                    return "Missed pickup";
                }

                return "Awaiting Release";
            }

            return row.Status;
        }
    }
}
