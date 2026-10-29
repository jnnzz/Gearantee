using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Locks;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Returns;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace ASI.Basecode.Services.Services
{
    /// <summary>Custodian return desk: record returns (WBS 44.00), condition and item status (45.00-46.00), and late returns (47.00).</summary>
    public class ReturnRecordService : IReturnRecordService
    {
        public const int PageSize = PaginationDefaults.PageSize;
        public const string OnLoanTab = "OnLoan";
        public const string OverdueTab = "Overdue";
        public const string ReturnedTab = "Returned";
        public const string LateTab = "Late";
        private const int NotesMaxLength = 1000;
        private const string NoPermissionMessage = "You no longer have permission to record returns.";
        private const string AlreadyReturnedMessage = "This item has already been returned. Reload the page to see the latest loans.";

        private readonly AsiBasecodeDBContext _db;
        private readonly IReturnRecordRepository _returnRecordRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IEquipmentItemRepository _equipmentItemRepository;
        private readonly TimeProvider _timeProvider;
        private readonly IApplicationLockManager _applicationLocks;

        public ReturnRecordService(
            IUnitOfWork unitOfWork,
            IReturnRecordRepository returnRecordRepository,
            IReservationRepository reservationRepository,
            IEquipmentItemRepository equipmentItemRepository,
            TimeProvider timeProvider)
        {
            _db = unitOfWork.Context;
            _returnRecordRepository = returnRecordRepository;
            _reservationRepository = reservationRepository;
            _equipmentItemRepository = equipmentItemRepository;
            _timeProvider = timeProvider;
            _applicationLocks = new ApplicationLockManager(_db);
        }

        public ReturnRecordIndexViewModel GetReturnRecords(string tab, int page)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var model = new ReturnRecordIndexViewModel { Tab = NormalizeTab(tab) };

            var loans = ActiveLoans();
            var overdue = loans.Where(reservation => reservation.ReservationEnd < nowUtc);
            var returns = _returnRecordRepository.GetReturnRecords().AsNoTracking();
            var late = returns.Where(record => record.LateReturn != null);

            model.Tabs.Add(new ReturnRecordTab { Value = OnLoanTab, Label = "On loan", Count = loans.Count() });
            model.Tabs.Add(new ReturnRecordTab { Value = OverdueTab, Label = "Overdue", Count = overdue.Count() });
            model.Tabs.Add(new ReturnRecordTab { Value = ReturnedTab, Label = "Returned", Count = returns.Count() });
            model.Tabs.Add(new ReturnRecordTab { Value = LateTab, Label = "Late returns", Count = late.Count() });

            model.TotalCount = model.Tabs.Single(item => item.Value == model.Tab).Count;
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)PageSize));
            model.Page = Math.Clamp(page, 1, model.TotalPages);
            var skip = (model.Page - 1) * PageSize;

            // Loans: most urgent due time first; returns: newest first
            var rows = model.Tab switch
            {
                OverdueTab => ProjectLoans(overdue.OrderBy(r => r.ReservationEnd).ThenBy(r => r.ReservationId).Skip(skip).Take(PageSize)),
                ReturnedTab => ProjectReturns(returns.OrderByDescending(r => r.ActualReturnAt).ThenByDescending(r => r.ReturnRecordId).Skip(skip).Take(PageSize)),
                LateTab => ProjectReturns(late.OrderByDescending(r => r.ActualReturnAt).ThenByDescending(r => r.ReturnRecordId).Skip(skip).Take(PageSize)),
                _ => ProjectLoans(loans.OrderBy(r => r.ReservationEnd).ThenBy(r => r.ReservationId).Skip(skip).Take(PageSize))
            };

            model.Items = rows.ToList()
                .Select(row => ToListItem(new ReturnRecordListItemViewModel(), row, nowUtc))
                .ToList();
            return model;
        }

        public ReturnRecordListItemViewModel RetrieveActiveLoan(long releaseRecordId)
        {
            var row = ProjectLoans(ActiveLoans()
                    .Where(reservation => reservation.ReleaseRecord.ReleaseRecordId == releaseRecordId))
                .FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            return ToListItem(new ReturnRecordListItemViewModel(), row, _timeProvider.GetUtcNow().UtcDateTime);
        }

        public ReturnRecordResult AddReturnRecord(ReturnRecordViewModel model, string userId)
        {
            if (model == null)
            {
                return ReturnRecordResult.Fail("The return form is incomplete. Reload and try again.");
            }

            var condition = ReturnInspection.Normalize(ReturnInspection.Conditions, model.ReturnedCondition);
            if (condition == null)
            {
                return ReturnRecordResult.Fail("Choose the condition found during inspection.");
            }

            var status = ReturnInspection.Normalize(ReturnInspection.ResultingStatuses, model.ResultingItemStatus);
            if (status == null)
            {
                return ReturnRecordResult.Fail("Choose the item's status after inspection.");
            }

            if (!ReturnInspection.IsAllowed(condition, status))
            {
                return ReturnRecordResult.Fail("Only items in Good condition can go back to Available. Choose Under Maintenance or Unavailable.");
            }

            if (!model.ActualReturnAt.HasValue)
            {
                return ReturnRecordResult.Fail("Enter the date and time the item came back.");
            }

            var notes = model.Notes?.Trim();
            if (notes?.Length > NotesMaxLength)
            {
                return ReturnRecordResult.Fail($"Inspection notes must be {NotesMaxLength} characters or fewer.");
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var returnedUtc = ManilaClock.ToUtc(model.ActualReturnAt.Value);
            if (returnedUtc > nowUtc)
            {
                return ReturnRecordResult.Fail("The return time can't be in the future.");
            }

            var loan = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.ReleaseRecord.ReleaseRecordId == model.ReleaseRecordId)
                .Select(reservation => new
                {
                    reservation.EquipmentId,
                    DueAt = reservation.ReservationEnd,
                    ReleasedAt = reservation.ReleaseRecord.ActualReleaseAt,
                    IsReturned = reservation.ReleaseRecord.ReturnRecord != null
                })
                .FirstOrDefault();
            if (loan == null)
            {
                return ReturnRecordResult.Missing();
            }

            if (loan.IsReturned)
            {
                return ReturnRecordResult.Fail(AlreadyReturnedMessage);
            }

            // Form input has minute precision, so compare against the release minute
            var releasedMinute = loan.ReleasedAt.AddTicks(-(loan.ReleasedAt.Ticks % TimeSpan.TicksPerMinute));
            if (returnedUtc < releasedMinute)
            {
                return ReturnRecordResult.Fail($"The return time can't be before the item was released ({ManilaClock.ToLocal(loan.ReleasedAt):d MMM yyyy Â· HH:mm}).");
            }

            // Back-dating stays possible but must be explained
            if (ReturnInspection.IsBackdated(returnedUtc, nowUtc) && string.IsNullOrEmpty(notes))
            {
                return ReturnRecordResult.Fail($"This return is dated more than {ReturnInspection.BackdateGraceMinutes} minutes ago. Add an inspection note explaining why it's being recorded late.");
            }

            // Lock order: equipment item row lock; no ReturnRecord range read here (avoids deadlocks)
            var strategy = _db.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
            // Rebuild state per attempt: a rolled-back attempt can leave the tracked item or
            // the new return record behind, which would corrupt the retry.
            _db.ChangeTracker.Clear();

            // Build a fresh return record (and its late-return child) on every attempt. A
            // rolled-back insert keeps its database-generated ID, so reusing the object would
            // replay it as an explicit identity insert on retry.
            var newReturnRecord = new ReturnRecord
            {
                ReleaseRecordId = model.ReleaseRecordId,
                ReceivedByUserId = userId,
                ActualReturnAt = returnedUtc,
                ReturnedCondition = condition,
                ResultingItemStatus = status,
                Notes = string.IsNullOrEmpty(notes) ? null : notes,
                CreatedAt = nowUtc
            };

            var daysLate = ReturnInspection.DaysLate(loan.DueAt, returnedUtc);
            if (daysLate > 0)
            {
                newReturnRecord.LateReturn = new LateReturn
                {
                    DueAt = loan.DueAt,
                    ReturnedAt = returnedUtc,
                    DaysLate = daysLate,
                    PenaltyStatus = DomainValues.PenaltyStatuses.Recorded,
                    CreatedAt = nowUtc
                };
            }

            DbUpdateException saveFailure = null;
            using (var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                // Take the administration lock in shared mode before the permission check.
                // Permission/role changes take it exclusively, so a revocation either lands
                // before this return is authorized or waits until it commits; it can never
                // slip in between the check and the write.
                if (_db.Database.IsSqlServer())
                {
                    _applicationLocks.AcquireShared(AdministrationLock.ResourceName);
                }

                if (!CanRecordReturns(userId))
                {
                    return ReturnRecordResult.Fail(NoPermissionMessage);
                }

                var lockedItem = _equipmentItemRepository.LoadForUpdate(loan.EquipmentId, includeArchived: true);
                if (lockedItem == null)
                {
                    return ReturnRecordResult.Missing();
                }

                lockedItem.ConditionStatus = condition;
                lockedItem.ItemStatus = status;
                lockedItem.UpdatedAt = nowUtc;

                try
                {
                    // One save writes the return, late return, and item changes
                    _returnRecordRepository.AddReturnRecord(newReturnRecord);
                    transaction.Commit();
                }
                catch (DbUpdateException exception)
                {
                    saveFailure = exception;
                }
            }

            if (saveFailure != null)
            {
                // Unique release index caught a concurrent return; anything else is rethrown
                _db.ChangeTracker.Clear();
                if (_returnRecordRepository.GetReturnRecords().Any(record => record.ReleaseRecordId == model.ReleaseRecordId))
                {
                    return ReturnRecordResult.Fail(AlreadyReturnedMessage);
                }

                ExceptionDispatchInfo.Capture(saveFailure).Throw();
            }

            return ReturnRecordResult.Success(
                daysLate > 0
                    ? $"Return recorded. The item came back {daysLate} day{(daysLate == 1 ? "" : "s")} late, so a late return was logged."
                    : "Return recorded.",
                newReturnRecord.ReturnRecordId);
            });
        }

        public ReturnRecordDetailsViewModel RetrieveReturnRecord(long returnRecordId)
        {
            var row = ProjectReturns(_returnRecordRepository.GetReturnRecords()
                    .AsNoTracking()
                    .Where(record => record.ReturnRecordId == returnRecordId))
                .FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            var details = ToListItem(new ReturnRecordDetailsViewModel(), row, _timeProvider.GetUtcNow().UtcDateTime);
            details.Location = row.Location;
            details.ResultingItemStatus = row.ResultingItemStatus;
            details.Notes = row.Notes;
            details.ReceivedByName = FullName(row.ReceiverFirstName, row.ReceiverLastName);
            details.PenaltyStatus = row.PenaltyStatus;
            details.RecordedAtLocal = ManilaClock.ToLocal(row.RecordedAt);
            details.IsBackdated = ReturnInspection.IsBackdated(row.ReturnedAt.Value, row.RecordedAt);
            return details;
        }

        // Released but not yet returned
        private IQueryable<Reservation> ActiveLoans()
        {
            return _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.ReleaseRecord != null && reservation.ReleaseRecord.ReturnRecord == null);
        }


        // DB-backed check; cookie claims aren't refreshed when grants change
        private bool CanRecordReturns(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return (
                from user in _db.Users.AsNoTracking()
                join userRole in _db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                join rolePermission in _db.RolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
                join permission in _db.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
                where user.Id == userId &&
                      user.IsActive &&
                      (role.Name == DomainValues.Roles.Custodian || role.Name == DomainValues.Roles.Administrator) &&
                      permission.PermissionName == DomainValues.Permissions.TransactionReleaseReturn
                select permission.PermissionId)
                .Any();
        }

        private static string NormalizeTab(string tab)
        {
            var tabs = new[] { OnLoanTab, OverdueTab, ReturnedTab, LateTab };
            return ReturnInspection.Normalize(tabs, tab) ?? OnLoanTab;
        }

        private static IQueryable<ReturnRow> ProjectLoans(IQueryable<Reservation> reservations)
        {
            return reservations.Select(reservation => new ReturnRow
            {
                ReleaseRecordId = reservation.ReleaseRecord.ReleaseRecordId,
                ReservationId = reservation.ReservationId,
                ItemCode = reservation.EquipmentItem.ItemCode,
                ItemName = reservation.EquipmentItem.ItemName,
                CategoryName = reservation.EquipmentItem.Category.CategoryName,
                Location = reservation.EquipmentItem.Location,
                BorrowerFirstName = reservation.BorrowerProfile.User.FirstName,
                BorrowerLastName = reservation.BorrowerProfile.User.LastName,
                SchoolId = reservation.BorrowerProfile.User.UserCode,
                Status = reservation.Status,
                ReleasedAt = reservation.ReleaseRecord.ActualReleaseAt,
                DueAt = reservation.ReservationEnd
            });
        }

        private static IQueryable<ReturnRow> ProjectReturns(IQueryable<ReturnRecord> records)
        {
            return records.Select(record => new ReturnRow
            {
                ReturnRecordId = record.ReturnRecordId,
                ReleaseRecordId = record.ReleaseRecordId,
                ReservationId = record.ReleaseRecord.ReservationId,
                ItemCode = record.ReleaseRecord.Reservation.EquipmentItem.ItemCode,
                ItemName = record.ReleaseRecord.Reservation.EquipmentItem.ItemName,
                CategoryName = record.ReleaseRecord.Reservation.EquipmentItem.Category.CategoryName,
                Location = record.ReleaseRecord.Reservation.EquipmentItem.Location,
                BorrowerFirstName = record.ReleaseRecord.Reservation.BorrowerProfile.User.FirstName,
                BorrowerLastName = record.ReleaseRecord.Reservation.BorrowerProfile.User.LastName,
                SchoolId = record.ReleaseRecord.Reservation.BorrowerProfile.User.UserCode,
                Status = record.ReleaseRecord.Reservation.Status,
                ReleasedAt = record.ReleaseRecord.ActualReleaseAt,
                DueAt = record.ReleaseRecord.Reservation.ReservationEnd,
                ReturnedAt = record.ActualReturnAt,
                RecordedAt = record.CreatedAt,
                ReturnedCondition = record.ReturnedCondition,
                ResultingItemStatus = record.ResultingItemStatus,
                Notes = record.Notes,
                ReceiverFirstName = record.ReceivedByUser.FirstName,
                ReceiverLastName = record.ReceivedByUser.LastName,
                DaysLate = record.LateReturn != null ? record.LateReturn.DaysLate : (int?)null,
                PenaltyStatus = record.LateReturn != null ? record.LateReturn.PenaltyStatus : null
            });
        }

        private static T ToListItem<T>(T item, ReturnRow row, DateTime nowUtc)
            where T : ReturnRecordListItemViewModel
        {
            item.ReturnRecordId = row.ReturnRecordId;
            item.ReleaseRecordId = row.ReleaseRecordId;
            item.ReservationId = row.ReservationId;
            item.ItemCode = row.ItemCode;
            item.ItemName = row.ItemName;
            item.CategoryName = row.CategoryName;
            item.BorrowerName = FullName(row.BorrowerFirstName, row.BorrowerLastName) ?? string.Empty;
            item.SchoolId = row.SchoolId ?? string.Empty;
            item.ReleasedAtLocal = ManilaClock.ToLocal(row.ReleasedAt);
            item.DueAtLocal = ManilaClock.ToLocal(row.DueAt);
            item.ReturnedAtLocal = row.ReturnedAt.HasValue ? ManilaClock.ToLocal(row.ReturnedAt.Value) : (DateTime?)null;
            item.ReturnedCondition = row.ReturnedCondition;
            item.DaysLate = row.DaysLate;
            item.DisplayStatus = ReservationDisplayStatus.For(row.Status, row.DueAt, row.ReleasedAt, row.ReturnedAt, nowUtc);
            return item;
        }

        private static string FullName(string firstName, string lastName)
        {
            var name = string.Join(" ", new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return name.Length > 0 ? name : null;
        }

        private sealed class ReturnRow
        {
            public long? ReturnRecordId { get; set; }
            public long ReleaseRecordId { get; set; }
            public long ReservationId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string CategoryName { get; set; }
            public string Location { get; set; }
            public string BorrowerFirstName { get; set; }
            public string BorrowerLastName { get; set; }
            public string SchoolId { get; set; }
            public string Status { get; set; }
            public DateTime ReleasedAt { get; set; }
            public DateTime DueAt { get; set; }
            public DateTime? ReturnedAt { get; set; }
            public DateTime RecordedAt { get; set; }
            public string ReturnedCondition { get; set; }
            public string ResultingItemStatus { get; set; }
            public string Notes { get; set; }
            public string ReceiverFirstName { get; set; }
            public string ReceiverLastName { get; set; }
            public int? DaysLate { get; set; }
            public string PenaltyStatus { get; set; }
        }
    }
}
