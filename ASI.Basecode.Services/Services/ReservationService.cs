using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace ASI.Basecode.Services.Services
{
    public class ReservationService : IReservationService
    {
        public const int PageSize = PaginationDefaults.PageSize;
        private const int PurposeMaxLength = 1000;
        private const int ReasonMaxLength = 1000;
        private const int NotesMaxLength = 1000;

        private const string DeniedMessage =
            "You no longer have permission to review reservations. Reload the page and try again.";

        private const string ChangedWhileReviewing =
            "This reservation changed while you were reviewing it. Reload the page to see its current status.";

        private const string ApprovedAction = "ReservationApproved";
        private const string RejectedAction = "ReservationRejected";
        private const string ReleasedAction = "EquipmentReleased";

        private static readonly string[] StatusTabs =
        {
            DomainValues.ReservationStatuses.Pending,
            DomainValues.ReservationStatuses.Approved,
            DomainValues.ReservationStatuses.Rejected,
            DomainValues.ReservationStatuses.Cancelled,
            DomainValues.ReservationStatuses.Expired
        };

        private readonly AsiBasecodeDBContext _db;
        private readonly IAuditService _auditService;
        private readonly IReservationRepository _reservationRepository;
        private readonly IEquipmentItemRepository _equipmentItemRepository;
        private readonly IBorrowerProfileRepository _borrowerProfileRepository;
        private readonly TimeProvider _timeProvider;

        public ReservationService(
            IUnitOfWork unitOfWork,
            IReservationRepository reservationRepository,
            IEquipmentItemRepository equipmentItemRepository,
            IBorrowerProfileRepository borrowerProfileRepository,
            IAuditService auditService,
            TimeProvider timeProvider)
        {
            _db = unitOfWork.Context;
            _reservationRepository = reservationRepository;
            _equipmentItemRepository = equipmentItemRepository;
            _borrowerProfileRepository = borrowerProfileRepository;
            _auditService = auditService;
            _timeProvider = timeProvider;
        }

        public string CheckBorrowerEligibility(string userId)
        {
            return EligibilityError(FindProfile(userId));
        }

        private static string EligibilityError(BorrowerProfileSummary profile)
        {
            if (profile == null)
            {
                return "Your account has no borrower profile yet. Ask an administrator to set one up before requesting equipment.";
            }

            if (!profile.IsActive)
            {
                return "Your account is deactivated, so you can't request equipment.";
            }

            if (!profile.IsEligible)
            {
                return "You're not currently eligible to borrow equipment. Ask an administrator to review your borrower profile.";
            }

            return null;
        }

        public ReservationResult SubmitReservation(ReservationCreateViewModel model, string userId)
        {
            if (model == null)
            {
                return ReservationResult.Fail("The reservation form is incomplete. Reload and try again.");
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var profile = FindProfile(userId);
            var eligibilityError = EligibilityError(profile);
            if (eligibilityError != null)
            {
                return ReservationResult.Fail(eligibilityError);
            }

            var windowError = ReservationAvailability.ValidateWindow(model.ReservationStart, model.ReservationEnd, nowUtc);
            if (windowError != null)
            {
                return ReservationResult.Fail(windowError);
            }

            var purpose = model.Purpose?.Trim();
            if (string.IsNullOrEmpty(purpose))
            {
                return ReservationResult.Fail("Tell the custodian what the equipment is for.");
            }

            if (purpose.Length > PurposeMaxLength)
            {
                return ReservationResult.Fail($"The purpose must be {PurposeMaxLength} characters or fewer.");
            }

            var startUtc = ManilaClock.ToUtc(model.ReservationStart.Value);
            var endUtc = ManilaClock.ToUtc(model.ReservationEnd.Value);

            var profileId = profile.BorrowerProfileId;

            var strategy = _db.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
                // Rebuild state per attempt so a rolled-back submit cannot reuse a tracked
                // reservation/item and misreport the result.
                _db.ChangeTracker.Clear();

                // Build a fresh reservation on every attempt. A rolled-back insert keeps its
                // database-generated ID, so reusing the object would replay it as an explicit
                // identity insert on retry.
                var newReservation = new Reservation
                {
                    BorrowerProfileId = profileId,
                    EquipmentId = model.EquipmentId,
                    ReservationStart = startUtc,
                    ReservationEnd = endUtc,
                    Purpose = purpose,
                    Status = DomainValues.ReservationStatuses.Pending,
                    RequestedAt = nowUtc,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                // Inventory changes and reservation submissions acquire the equipment row lock
                // first. All reservation checks and the insert stay inside that serializable
                // boundary, so a concurrent archive/status change cannot invalidate an earlier
                // availability check (CONC-01).
                using (var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var lockedItem = _equipmentItemRepository.LoadForUpdate(model.EquipmentId);
                    if (lockedItem == null)
                    {
                        return ReservationResult.Fail("This item can't be reserved right now. It may be under maintenance, unavailable, or archived.");
                    }

                    // D3: one open request per borrower, item, and window. Acquire this second lock
                    // only after the shared item lock; the order is consistent across submissions.
                    _reservationRepository.AcquireSubmissionLock(profileId, model.EquipmentId);

                    var itemIsReservable = _equipmentItemRepository.GetEquipmentItems()
                        .AsNoTracking()
                        .Where(ReservationAvailability.IsReservable())
                        .Any(item => item.EquipmentId == model.EquipmentId);
                    if (!itemIsReservable)
                    {
                        return ReservationResult.Fail("This item can't be reserved right now. It may be under maintenance, unavailable, or archived.");
                    }

                    var isBooked = _reservationRepository.GetReservations()
                        .Where(reservation => reservation.EquipmentId == model.EquipmentId)
                        .Any(ReservationAvailability.Blocks(startUtc, endUtc, nowUtc));
                    if (isBooked)
                    {
                        return ReservationResult.Fail("This item is already booked or still on loan during that time. Pick a time outside the booked times.");
                    }

                    // A Pending request whose start has passed is Expired (D2), so it no longer
                    // counts. The check and insert remain protected by both locks (RES-01).
                    var hasDuplicate = _reservationRepository.GetReservations()
                        .Any(reservation =>
                            reservation.BorrowerProfileId == profileId &&
                            reservation.EquipmentId == model.EquipmentId &&
                            reservation.Status == DomainValues.ReservationStatuses.Pending &&
                            reservation.ReservationStart > nowUtc &&
                            reservation.ReservationStart < endUtc &&
                            reservation.ReservationEnd > startUtc);
                    if (hasDuplicate)
                    {
                        return ReservationResult.Fail("You already have a pending request for this item that overlaps this time.");
                    }

                    _reservationRepository.AddReservation(newReservation);
                    transaction.Commit();
                }

                return ReservationResult.Success(
                    "Reservation request submitted. A custodian will review it.",
                    newReservation.ReservationId);
            });
        }

        public ReservationIndexViewModel GetMyReservations(string userId, string status, int page)
        {
            var model = new ReservationIndexViewModel();
            var profile = FindProfile(userId);
            var statusFilter = NormalizeStatus(status);
            model.StatusFilter = statusFilter ?? string.Empty;

            var counts = new Dictionary<string, int>();
            if (profile != null)
            {
                var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                ExpireStalePending(profile.BorrowerProfileId, nowUtc);

                var mine = _reservationRepository.GetReservations()
                    .AsNoTracking()
                    .Where(reservation => reservation.BorrowerProfileId == profile.BorrowerProfileId);

                counts = mine
                    .GroupBy(reservation => reservation.Status)
                    .Select(group => new { Status = group.Key, Count = group.Count() })
                    .ToDictionary(group => group.Status, group => group.Count);

                if (statusFilter != null)
                {
                    mine = mine.Where(reservation => reservation.Status == statusFilter);
                }

                model.TotalCount = mine.Count();
                model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)PageSize));
                model.Page = Math.Clamp(page, 1, model.TotalPages);

                model.Items = Project(mine
                        .OrderByDescending(reservation => reservation.RequestedAt)
                        .ThenByDescending(reservation => reservation.ReservationId)
                        .Skip((model.Page - 1) * PageSize)
                        .Take(PageSize))
                    .ToList()
                    .Select(row => ToListItem(new ReservationListItemViewModel(), row, nowUtc))
                    .ToList();
            }

            model.Tabs.Add(new ReservationStatusTab { Label = "All", Count = counts.Values.Sum() });
            foreach (var tabStatus in StatusTabs)
            {
                model.Tabs.Add(new ReservationStatusTab
                {
                    Value = tabStatus,
                    Label = tabStatus,
                    Count = counts.TryGetValue(tabStatus, out var count) ? count : 0
                });
            }

            return model;
        }

        public ReservationDetailsViewModel RetrieveMyReservation(long reservationId, string userId)
        {
            var profile = FindProfile(userId);
            if (profile == null)
            {
                return null;
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            ExpireStalePending(profile.BorrowerProfileId, nowUtc);

            var row = Project(_reservationRepository.GetReservations()
                    .AsNoTracking()
                    .Where(reservation =>
                        reservation.ReservationId == reservationId &&
                        reservation.BorrowerProfileId == profile.BorrowerProfileId))
                .FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            var reviewerName = string.Join(" ", new[] { row.ReviewerFirstName, row.ReviewerLastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            var details = ToListItem(new ReservationDetailsViewModel(), row, nowUtc);
            details.Brand = row.Brand;
            details.Model = row.Model;
            details.Location = row.Location;
            details.ImageUrl = row.ImageUrl;
            details.Purpose = row.Purpose;
            details.RejectionReason = row.RejectionReason;
            details.ReviewerName = reviewerName.Length > 0 ? reviewerName : null;
            details.ReviewedAtLocal = ToLocal(row.ReviewedAt);
            details.CancelledAtLocal = ToLocal(row.CancelledAt);
            details.ReleasedAtLocal = ToLocal(row.ReleasedAt);
            details.ReturnedAtLocal = ToLocal(row.ReturnedAt);
            details.ReturnedCondition = row.ReturnedCondition;
            return details;
        }

        public ReservationResult CancelReservation(long reservationId, string userId)
        {
            var profile = FindProfile(userId);
            if (profile == null)
            {
                return ReservationResult.Missing();
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            ExpireStalePending(profile.BorrowerProfileId, nowUtc);

            var current = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation =>
                    reservation.ReservationId == reservationId &&
                    reservation.BorrowerProfileId == profile.BorrowerProfileId)
                .Select(reservation => new
                {
                    reservation.Status,
                    HasRelease = reservation.ReleaseRecord != null,
                    IsReturned = reservation.ReleaseRecord != null && reservation.ReleaseRecord.ReturnRecord != null
                })
                .FirstOrDefault();
            if (current == null)
            {
                return ReservationResult.Missing();
            }

            var refusal = CancelRefusal(current.Status, current.HasRelease, current.IsReturned);
            if (refusal != null)
            {
                return ReservationResult.Fail(refusal);
            }

            // D4, checked again inside the UPDATE itself so a custodian releasing the item
            // at the same moment can't leave a cancelled reservation with a release record.
            var cancelled = _reservationRepository.GetReservations()
                .Where(reservation =>
                    reservation.ReservationId == reservationId &&
                    reservation.BorrowerProfileId == profile.BorrowerProfileId &&
                    ((reservation.Status == DomainValues.ReservationStatuses.Pending &&
                        reservation.ReservationStart > nowUtc) ||
                     (reservation.Status == DomainValues.ReservationStatuses.Approved &&
                        reservation.ReleaseRecord == null)))
                .ExecuteUpdate(setters => setters
                    .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Cancelled)
                    .SetProperty(reservation => reservation.CancelledAt, nowUtc)
                    .SetProperty(reservation => reservation.UpdatedAt, nowUtc));
            if (cancelled == 0)
            {
                return ReservationResult.Fail("This reservation changed while you were cancelling it. Reload the page to see its current status.");
            }

            return ReservationResult.Success("Reservation cancelled.", reservationId);
        }

        public ReservationReviewIndexViewModel GetReviewQueue(string status, string search, int page)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            ExpireStalePendingForAll(nowUtc);

            var model = new ReservationReviewIndexViewModel
            {
                StatusFilter = NormalizeStatus(status) ?? string.Empty,
                Search = search?.Trim() ?? string.Empty
            };

            var queue = _reservationRepository.GetReservations().AsNoTracking();

            var counts = queue
                .GroupBy(reservation => reservation.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToDictionary(group => group.Status, group => group.Count);

            var term = model.Search.ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                queue = queue.Where(reservation =>
                    reservation.EquipmentItem.ItemCode.ToLower().Contains(term) ||
                    reservation.EquipmentItem.ItemName.ToLower().Contains(term) ||
                    reservation.BorrowerProfile.User.UserCode.ToLower().Contains(term) ||
                    reservation.BorrowerProfile.User.FirstName.ToLower().Contains(term) ||
                    reservation.BorrowerProfile.User.LastName.ToLower().Contains(term));
            }

            if (!string.IsNullOrEmpty(model.StatusFilter))
            {
                queue = queue.Where(reservation => reservation.Status == model.StatusFilter);
            }

            model.TotalCount = queue.Count();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalCount / (double)PageSize));
            model.Page = Math.Clamp(page, 1, model.TotalPages);

            model.Items = Project(queue
                    .OrderBy(reservation => reservation.ReservationStart)
                    .ThenBy(reservation => reservation.ReservationId)
                    .Skip((model.Page - 1) * PageSize)
                    .Take(PageSize))
                .ToList()
                .Select(row => ToReviewListItem(new ReservationReviewListItemViewModel(), row, nowUtc))
                .ToList();

            model.Tabs.Add(new ReservationStatusTab { Label = "All", Count = counts.Values.Sum() });
            foreach (var tabStatus in StatusTabs)
            {
                model.Tabs.Add(new ReservationStatusTab
                {
                    Value = tabStatus,
                    Label = tabStatus,
                    Count = counts.TryGetValue(tabStatus, out var count) ? count : 0
                });
            }

            return model;
        }

        public ReservationReviewDetailsViewModel RetrieveForReview(long reservationId)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            ExpireStalePendingForAll(nowUtc);

            var row = Project(_reservationRepository.GetReservations()
                    .AsNoTracking()
                    .Where(reservation => reservation.ReservationId == reservationId))
                .FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            return ToReviewDetails(new ReservationReviewDetailsViewModel(), row, nowUtc);
        }

        public ReservationResult ApproveReservation(string actorUserId, long reservationId)
        {
            // Fast rejection only. The authoritative check is repeated under the shared
            // administration lock in the write transaction below.
            if (!CanReview(actorUserId, DomainValues.Permissions.ReservationReview))
            {
                return ReservationResult.Denied(DeniedMessage);
            }

            // Read only the equipment key before the transaction so the item row can be
            // locked before reservation state/range reads (same lock order as submissions).
            var equipmentId = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.ReservationId == reservationId)
                .Select(reservation => (long?)reservation.EquipmentId)
                .SingleOrDefault();
            if (!equipmentId.HasValue)
            {
                return ReservationResult.Missing();
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
            // Rebuild state per attempt so a rolled-back approval cannot reuse tracked state.
            _db.ChangeTracker.Clear();

            using (var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!CanReview(actorUserId, DomainValues.Permissions.ReservationReview))
                {
                    return ReservationResult.Denied(DeniedMessage);
                }

                // Lock ordering: equipment row lock -> reservation state.
                var item = _equipmentItemRepository.LoadForUpdate(equipmentId.Value);
                if (item == null)
                {
                    return ReservationResult.Fail("This item is archived or no longer exists, so its request can't be approved.");
                }

                var state = LoadReviewState(reservationId);
                if (state == null)
                {
                    return ReservationResult.Missing();
                }

                if (state.EquipmentId != equipmentId.Value)
                {
                    return ReservationResult.Fail(ChangedWhileReviewing);
                }

                if (state.Status != DomainValues.ReservationStatuses.Pending)
                {
                    return ReservationResult.Fail(DecisionRefusal("approve", state.Status));
                }

                // Capture the current time only after the transaction has acquired the
                // administration and equipment locks and reloaded the reservation state.
                // This prevents an old timestamp from approving a request whose start time
                // passes while the operation is waiting for a lock.
                var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

                // A page can remain open past the requested start time. Expire the Pending row
                // inside this transaction rather than allowing a stale form to approve it.
                if (state.ReservationStart <= nowUtc)
                {
                    var expired = _reservationRepository.GetReservations()
                        .Where(reservation =>
                            reservation.ReservationId == reservationId &&
                            reservation.Status == DomainValues.ReservationStatuses.Pending &&
                            reservation.ReservationStart <= nowUtc)
                        .ExecuteUpdate(setters => setters
                            .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Expired)
                            .SetProperty(reservation => reservation.UpdatedAt, nowUtc));

                    if (expired == 0)
                    {
                        return ReservationResult.Fail(ChangedWhileReviewing);
                    }

                    transaction.Commit();
                    return ReservationResult.Fail("This request expired before it was reviewed. Reload the page to see its current status.");
                }

                // Reapply the exact rule used by the borrower availability workflow after
                // acquiring the item lock: not archived, category active, and Available/Borrowed.
                var isReservable = _equipmentItemRepository.GetEquipmentItems()
                    .AsNoTracking()
                    .Where(ReservationAvailability.IsReservable())
                    .Any(candidate => candidate.EquipmentId == state.EquipmentId);
                if (!isReservable)
                {
                    return ReservationResult.Fail("This item is no longer available for lending, or its category is inactive.");
                }

                var isBooked = _reservationRepository.GetReservations()
                    .Where(reservation => reservation.EquipmentId == state.EquipmentId)
                    .Any(ReservationAvailability.Blocks(state.ReservationStart, state.ReservationEnd, nowUtc));
                if (isBooked)
                {
                    return ReservationResult.Fail("This item is already booked or on loan during that window, so the request can't be approved.");
                }

                var approved = _reservationRepository.GetReservations()
                    .Where(reservation =>
                        reservation.ReservationId == reservationId &&
                        reservation.Status == DomainValues.ReservationStatuses.Pending &&
                        reservation.ReservationStart > nowUtc)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Approved)
                        .SetProperty(reservation => reservation.ReviewedByUserId, actorUserId)
                        .SetProperty(reservation => reservation.ReviewedAt, nowUtc)
                        .SetProperty(reservation => reservation.UpdatedAt, nowUtc));
                if (approved == 0)
                {
                    return ReservationResult.Fail(ChangedWhileReviewing);
                }

                AddAudit(actorUserId, state.BorrowerUserId, ApprovedAction, new { ReservationId = reservationId });
                _db.SaveChanges();
                transaction.Commit();
            }

            return ReservationResult.Success(
                "Reservation approved. The borrower can pick the item up.",
                reservationId);
            });
        }
        public ReservationResult RejectReservation(string actorUserId, long reservationId, string reason)
        {
            if (!CanReview(actorUserId, DomainValues.Permissions.ReservationReview))
            {
                return ReservationResult.Denied(DeniedMessage);
            }

            var trimmed = reason?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return ReservationResult.Fail("Tell the borrower why the request is being rejected.");
            }

            if (trimmed.Length > ReasonMaxLength)
            {
                return ReservationResult.Fail($"The reason must be {ReasonMaxLength} characters or fewer.");
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var strategy = _db.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
            // Rebuild state per attempt so a rolled-back rejection cannot reuse tracked state.
            _db.ChangeTracker.Clear();

            using (var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!CanReview(actorUserId, DomainValues.Permissions.ReservationReview))
                {
                    return ReservationResult.Denied(DeniedMessage);
                }

                var state = LoadReviewState(reservationId);
                if (state == null)
                {
                    return ReservationResult.Missing();
                }

                if (state.Status != DomainValues.ReservationStatuses.Pending)
                {
                    return ReservationResult.Fail(DecisionRefusal("reject", state.Status));
                }

                var rejected = _reservationRepository.GetReservations()
                    .Where(reservation =>
                        reservation.ReservationId == reservationId &&
                        reservation.Status == DomainValues.ReservationStatuses.Pending)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Rejected)
                        .SetProperty(reservation => reservation.RejectionReason, trimmed)
                        .SetProperty(reservation => reservation.ReviewedByUserId, actorUserId)
                        .SetProperty(reservation => reservation.ReviewedAt, nowUtc)
                        .SetProperty(reservation => reservation.UpdatedAt, nowUtc));
                if (rejected == 0)
                {
                    return ReservationResult.Fail(ChangedWhileReviewing);
                }

                AddAudit(actorUserId, state.BorrowerUserId, RejectedAction, new
                {
                    ReservationId = reservationId,
                    Reason = trimmed
                });
                _db.SaveChanges();
                transaction.Commit();
            }

            return ReservationResult.Success("Reservation rejected.", reservationId);
            });
        }

        public ReservationResult ReleaseEquipment(string actorUserId, long reservationId, string notes)
        {
            if (!CanReview(actorUserId, DomainValues.Permissions.TransactionReleaseReturn))
            {
                return ReservationResult.Denied(DeniedMessage);
            }

            var trimmedNotes = notes?.Trim();
            if (trimmedNotes?.Length > NotesMaxLength)
            {
                return ReservationResult.Fail($"The notes must be {NotesMaxLength} characters or fewer.");
            }

            // As with approval, obtain only the equipment key before the transaction so the
            // item lock is acquired before reservation state/range reads.
            var equipmentId = _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.ReservationId == reservationId)
                .Select(reservation => (long?)reservation.EquipmentId)
                .SingleOrDefault();
            if (!equipmentId.HasValue)
            {
                return ReservationResult.Missing();
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            var strategy = _db.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
            // Rebuild state per attempt so a rolled-back release cannot reuse tracked state.
            _db.ChangeTracker.Clear();

            using (var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!CanReview(actorUserId, DomainValues.Permissions.TransactionReleaseReturn))
                {
                    return ReservationResult.Denied(DeniedMessage);
                }

                // Lock ordering: equipment row lock -> reservation state.
                var item = _equipmentItemRepository.LoadForUpdate(equipmentId.Value);
                if (item == null)
                {
                    return ReservationResult.Fail("This item is archived or no longer exists, so its release can't be recorded.");
                }

                var state = LoadReviewState(reservationId);
                if (state == null)
                {
                    return ReservationResult.Missing();
                }

                if (state.EquipmentId != equipmentId.Value)
                {
                    return ReservationResult.Fail(ChangedWhileReviewing);
                }

                if (state.Status != DomainValues.ReservationStatuses.Approved)
                {
                    return ReservationResult.Fail(
                        state.Status == DomainValues.ReservationStatuses.Pending
                            ? "Approve this request before recording the release."
                            : "Only an approved reservation that hasn't been released can be handed over.");
                }

                if (state.HasRelease)
                {
                    return ReservationResult.Fail("This item has already been released for this reservation.");
                }

                if (!ReservationAvailability.IsReservableStatus(item.ItemStatus))
                {
                    return ReservationResult.Fail($"This item is marked {item.ItemStatus}, so it can't be released.");
                }

                var onLoanElsewhere = _reservationRepository.GetReservations()
                    .Any(reservation =>
                        reservation.EquipmentId == state.EquipmentId &&
                        reservation.ReservationId != reservationId &&
                        reservation.ReleaseRecord != null &&
                        reservation.ReleaseRecord.ReturnRecord == null);
                if (onLoanElsewhere)
                {
                    return ReservationResult.Fail("This item is still on loan to another borrower, so it can't be released again yet.");
                }

                _db.ReleaseRecords.Add(new ReleaseRecord
                {
                    ReservationId = reservationId,
                    ReleasedByUserId = actorUserId,
                    ActualReleaseAt = nowUtc,
                    Notes = string.IsNullOrEmpty(trimmedNotes) ? null : trimmedNotes,
                    CreatedAt = nowUtc
                });

                item.ItemStatus = DomainValues.EquipmentStatuses.Borrowed;
                item.UpdatedAt = nowUtc;

                AddAudit(actorUserId, state.BorrowerUserId, ReleasedAction, new { ReservationId = reservationId });

                try
                {
                    _db.SaveChanges();
                }
                catch (DbUpdateException)
                {
                    transaction.Rollback();
                    return ReservationResult.Fail("This item has already been released for this reservation.");
                }

                transaction.Commit();
            }

            return ReservationResult.Success(
                "Release recorded. The item is now marked as borrowed.",
                reservationId);
            });
        }

        private bool CanReview(string actorUserId, string permissionName)
        {
            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                return false;
            }

            var actor = _db.Users.AsNoTracking().SingleOrDefault(user => user.Id == actorUserId);
            if (actor == null || !actor.IsActive)
            {
                return false;
            }

            return (from userRole in _db.UserRoles.AsNoTracking()
                    join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    join rolePermission in _db.RolePermissions.AsNoTracking()
                        on role.Id equals rolePermission.RoleId
                    join permission in _db.Permissions.AsNoTracking()
                        on rolePermission.PermissionId equals permission.PermissionId
                    where userRole.UserId == actorUserId &&
                          role.Name == DomainValues.Roles.Custodian &&
                          permission.PermissionName == permissionName
                    select permission.PermissionId)
                .Any();
        }

        private ReviewState LoadReviewState(long reservationId)
        {
            return _reservationRepository.GetReservations()
                .AsNoTracking()
                .Where(reservation => reservation.ReservationId == reservationId)
                .Select(reservation => new ReviewState
                {
                    Status = reservation.Status,
                    EquipmentId = reservation.EquipmentId,
                    ReservationStart = reservation.ReservationStart,
                    ReservationEnd = reservation.ReservationEnd,
                    BorrowerUserId = reservation.BorrowerProfile.UserId,
                    HasRelease = reservation.ReleaseRecord != null
                })
                .FirstOrDefault();
        }

        private static string DecisionRefusal(string decision, string status) =>
            status switch
            {
                DomainValues.ReservationStatuses.Approved => $"This request was already approved, so there's nothing to {decision}.",
                DomainValues.ReservationStatuses.Rejected => $"This request was already rejected, so there's nothing to {decision}.",
                DomainValues.ReservationStatuses.Cancelled => $"The borrower cancelled this request, so there's nothing to {decision}.",
                DomainValues.ReservationStatuses.Expired => $"This request expired before it was reviewed, so there's nothing to {decision}.",
                _ => $"This request can't be {decision}d in its current state."
            };

        /// <summary>
        /// The queue must not offer a request whose start has already passed, so the same lazy
        /// expiry the borrower screens use is applied across every borrower.
        /// </summary>
        private void ExpireStalePendingForAll(DateTime nowUtc)
        {
            _reservationRepository.GetReservations()
                .Where(reservation =>
                    reservation.Status == DomainValues.ReservationStatuses.Pending &&
                    reservation.ReservationStart <= nowUtc)
                .ExecuteUpdate(setters => setters
                    .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Expired)
                    .SetProperty(reservation => reservation.UpdatedAt, nowUtc));
        }

        private static ReservationReviewListItemViewModel ToReviewListItem(
            ReservationReviewListItemViewModel item,
            ReservationRow row,
            DateTime nowUtc)
        {
            ToListItem(item, row, nowUtc);

            item.BorrowerName = string.Join(" ", new[] { row.BorrowerFirstName, row.BorrowerLastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            item.SchoolId = row.SchoolId ?? string.Empty;
            item.Department = row.Department ?? string.Empty;
            item.Purpose = row.Purpose ?? string.Empty;
            item.CanApprove = row.Status == DomainValues.ReservationStatuses.Pending;
            item.CanReject = row.Status == DomainValues.ReservationStatuses.Pending;
            item.CanRelease = row.Status == DomainValues.ReservationStatuses.Approved && !row.HasRelease;
            return item;
        }

        private static ReservationReviewDetailsViewModel ToReviewDetails(
            ReservationReviewDetailsViewModel item,
            ReservationRow row,
            DateTime nowUtc)
        {
            ToReviewListItem(item, row, nowUtc);

            var reviewerName = string.Join(" ", new[] { row.ReviewerFirstName, row.ReviewerLastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            item.Brand = row.Brand;
            item.Model = row.Model;
            item.Location = row.Location;
            item.ImageUrl = row.ImageUrl;
            item.RejectionReason = row.RejectionReason;
            item.ReviewerName = reviewerName.Length > 0 ? reviewerName : null;
            item.ReviewedAtLocal = ToLocal(row.ReviewedAt);
            item.ReleasedAtLocal = ToLocal(row.ReleasedAt);
            item.ReleaseNotes = row.ReleaseNotes;
            return item;
        }

        private void AddAudit(string actorUserId, string targetUserId, string action, object details)
        {
            _auditService.Stage(actorUserId, targetUserId, action, details);
        }

        private sealed class ReviewState
        {
            public string Status { get; set; }
            public long EquipmentId { get; set; }
            public DateTime ReservationStart { get; set; }
            public DateTime ReservationEnd { get; set; }
            public string BorrowerUserId { get; set; }
            public bool HasRelease { get; set; }
        }
        /// <summary>Why a reservation can't be cancelled, or null when it can (D4).</summary>
        private static string CancelRefusal(string status, bool hasRelease, bool isReturned)
        {
            if (isReturned)
            {
                return "This reservation is complete. The item has already been returned.";
            }

            if (hasRelease)
            {
                return "You've already picked up this item, so the reservation can't be cancelled. Return it to the custodian instead.";
            }

            return status switch
            {
                DomainValues.ReservationStatuses.Pending => null,
                DomainValues.ReservationStatuses.Approved => null,
                DomainValues.ReservationStatuses.Cancelled => "This reservation is already cancelled.",
                DomainValues.ReservationStatuses.Rejected => "This request was rejected, so there's nothing to cancel.",
                DomainValues.ReservationStatuses.Expired => "This request expired before it was reviewed, so there's nothing to cancel.",
                _ => "This reservation can't be cancelled."
            };
        }

        /// <summary>
        /// D2: a Pending request whose start has passed was never reviewed in time, so it
        /// becomes Expired. Written lazily whenever the borrower's reservations are read.
        /// </summary>
        private void ExpireStalePending(long borrowerProfileId, DateTime nowUtc)
        {
            // One conditional UPDATE instead of saving loaded rows: the Pending check runs at
            // write time, so a request a custodian approved after we loaded the page is never
            // overwritten with Expired (RES-02), and only Status and UpdatedAt change.
            _reservationRepository.GetReservations()
                .Where(reservation =>
                    reservation.BorrowerProfileId == borrowerProfileId &&
                    reservation.Status == DomainValues.ReservationStatuses.Pending &&
                    reservation.ReservationStart <= nowUtc)
                .ExecuteUpdate(setters => setters
                    .SetProperty(reservation => reservation.Status, DomainValues.ReservationStatuses.Expired)
                    .SetProperty(reservation => reservation.UpdatedAt, nowUtc));
        }

        private static string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            return StatusTabs.FirstOrDefault(tab => string.Equals(tab, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static IQueryable<ReservationRow> Project(IQueryable<Reservation> reservations)
        {
            return reservations.Select(reservation => new ReservationRow
            {
                ReservationId = reservation.ReservationId,
                EquipmentId = reservation.EquipmentId,
                ItemCode = reservation.EquipmentItem.ItemCode,
                ItemName = reservation.EquipmentItem.ItemName,
                CategoryName = reservation.EquipmentItem.Category.CategoryName,
                Brand = reservation.EquipmentItem.Brand,
                Model = reservation.EquipmentItem.Model,
                Location = reservation.EquipmentItem.Location,
                ImageUrl = reservation.EquipmentItem.ImageUrl,
                ReservationStart = reservation.ReservationStart,
                ReservationEnd = reservation.ReservationEnd,
                RequestedAt = reservation.RequestedAt,
                Status = reservation.Status,
                Purpose = reservation.Purpose,
                RejectionReason = reservation.RejectionReason,
                ReviewerFirstName = reservation.ReviewedByUser.FirstName,
                ReviewerLastName = reservation.ReviewedByUser.LastName,
                ReviewedAt = reservation.ReviewedAt,
                CancelledAt = reservation.CancelledAt,
                HasRelease = reservation.ReleaseRecord != null,
                ReleasedAt = reservation.ReleaseRecord != null
                    ? reservation.ReleaseRecord.ActualReleaseAt
                    : (DateTime?)null,
                ReturnedAt = reservation.ReleaseRecord != null && reservation.ReleaseRecord.ReturnRecord != null
                    ? reservation.ReleaseRecord.ReturnRecord.ActualReturnAt
                    : (DateTime?)null,
                ReturnedCondition = reservation.ReleaseRecord != null && reservation.ReleaseRecord.ReturnRecord != null
                    ? reservation.ReleaseRecord.ReturnRecord.ReturnedCondition
                    : null,
                BorrowerFirstName = reservation.BorrowerProfile.User.FirstName,
                BorrowerLastName = reservation.BorrowerProfile.User.LastName,
                SchoolId = reservation.BorrowerProfile.User.UserCode,
                Department = reservation.BorrowerProfile.Department,
                ReleaseNotes = reservation.ReleaseRecord != null
                    ? reservation.ReleaseRecord.Notes
                    : null
            });
        }

        private static T ToListItem<T>(T item, ReservationRow row, DateTime nowUtc)
            where T : ReservationListItemViewModel
        {
            item.ReservationId = row.ReservationId;
            item.EquipmentId = row.EquipmentId;
            item.ItemCode = row.ItemCode;
            item.ItemName = row.ItemName;
            item.CategoryName = row.CategoryName;
            item.StartLocal = ManilaClock.ToLocal(row.ReservationStart);
            item.EndLocal = ManilaClock.ToLocal(row.ReservationEnd);
            item.RequestedAtLocal = ManilaClock.ToLocal(row.RequestedAt);
            item.Status = row.Status;
            item.DisplayStatus = ReservationDisplayStatus.For(row.Status, row.ReservationEnd, row.ReleasedAt, row.ReturnedAt, nowUtc);
            item.CanCancel = CancelRefusal(row.Status, row.HasRelease, row.ReturnedAt.HasValue) == null;
            return item;
        }

        private static DateTime? ToLocal(DateTime? utc)
        {
            return utc.HasValue ? ManilaClock.ToLocal(utc.Value) : (DateTime?)null;
        }

        private BorrowerProfileSummary FindProfile(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            return _borrowerProfileRepository.GetBorrowerProfiles()
                .AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => new BorrowerProfileSummary
                {
                    BorrowerProfileId = profile.BorrowerProfileId,
                    IsEligible = profile.IsEligible,
                    IsActive = profile.User.IsActive
                })
                .FirstOrDefault();
        }

        private sealed class ReservationRow
        {
            public long ReservationId { get; set; }
            public long EquipmentId { get; set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string CategoryName { get; set; }
            public string Brand { get; set; }
            public string Model { get; set; }
            public string Location { get; set; }
            public string ImageUrl { get; set; }
            public DateTime ReservationStart { get; set; }
            public DateTime ReservationEnd { get; set; }
            public DateTime RequestedAt { get; set; }
            public string Status { get; set; }
            public string Purpose { get; set; }
            public string RejectionReason { get; set; }
            public string ReviewerFirstName { get; set; }
            public string ReviewerLastName { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public DateTime? CancelledAt { get; set; }
            public bool HasRelease { get; set; }
            public DateTime? ReleasedAt { get; set; }
            public DateTime? ReturnedAt { get; set; }
            public string ReturnedCondition { get; set; }
            public string BorrowerFirstName { get; set; }
            public string BorrowerLastName { get; set; }
            public string SchoolId { get; set; }
            public string Department { get; set; }
            public string ReleaseNotes { get; set; }
        }

        private sealed class BorrowerProfileSummary
        {
            public long BorrowerProfileId { get; set; }
            public bool IsEligible { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
