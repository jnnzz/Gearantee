using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Locks;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class BorrowerProfileService : ServiceBase, IBorrowerProfileService
    {
        private const int PageSize = PaginationDefaults.PageSize;

        private const string StaleMessage =
            "This borrower record changed after you opened it. Reload the page and try again.";

        private const string DeniedMessage =
            "You no longer have permission to maintain borrower profiles. Reload the page and try again.";

        private const string AuditFailureMessage =
            "The change could not be recorded in the audit trail, so nothing was saved.";

        private const string ProfileUpdatedAction = "BorrowerProfileUpdated";
        private const string EligibilityChangedAction = "BorrowerEligibilityChanged";

        private readonly AsiBasecodeDBContext _db;
        private readonly IAuditService _auditService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IApplicationLockManager _applicationLocks;

        public BorrowerProfileService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService,
            ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _db = unitOfWork.Context;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _auditService = auditService;
            _applicationLocks = new ApplicationLockManager(_db);
        }

        public async Task<BorrowerProfileListViewModel> GetBorrowerProfilesAsync(string search, int page)
        {
            IQueryable<BorrowerProfile> query = _db.BorrowerProfiles.AsNoTracking();

            var term = search?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(profile =>
                    profile.User.UserName.ToLower().Contains(term) ||
                    profile.Department.ToLower().Contains(term) ||
                    profile.User.FirstName.ToLower().Contains(term) ||
                    profile.User.LastName.ToLower().Contains(term));
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            var currentPage = Math.Min(Math.Max(1, page), totalPages);

            var borrowers = await Project(query
                    .OrderBy(profile => profile.User.UserName))
                .Skip((currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return new BorrowerProfileListViewModel
            {
                Borrowers = borrowers,
                Search = search,
                Page = currentPage,
                TotalPages = totalPages,
                TotalCount = totalCount
            };
        }

        public async Task<BorrowerProfileViewModel> GetBorrowerProfileAsync(long id)
        {
            return await Project(
                    _db.BorrowerProfiles.AsNoTracking()
                        .Where(profile => profile.BorrowerProfileId == id))
                .SingleOrDefaultAsync();
        }

        public async Task<BorrowerProfileResult> UpdateBorrowerProfileAsync(
            string actorUserId,
            long id,
            BorrowerProfileUpdateViewModel model)
        {
            if (model == null)
            {
                return BorrowerProfileResult.Failure(
                    "The update form was incomplete. Reload and try again.");
            }

            if (string.IsNullOrWhiteSpace(model.Department))
            {
                return BorrowerProfileResult.Failure("Department is required.");
            }

            // The authorization check runs inside the same transaction as the write.
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
            // Rebuild state on every attempt: a failed attempt can leave uncommitted values
            // tracked, which would make the retry report a stale/unchanged result instead of
            // reading the committed row.
            _db.ChangeTracker.Clear();

            await using var transaction = await BeginAdministrativeWriteAsync();
            if (!await CanManageAsync(actorUserId))
            {
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Denied(DeniedMessage);
            }

            var profile = await _db.BorrowerProfiles
                .SingleOrDefaultAsync(item => item.BorrowerProfileId == id);
            if (profile == null)
            {
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Missing("Borrower record not found.");
            }

            // The row is identified only by "id"; the stamp is what proves the posted form is
            // still current. Without this check, a form opened before another administrator
            // saved would silently overwrite the newer values.
            if (!IsCurrent(profile, model.ConcurrencyStamp))
            {
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Stale(StaleMessage);
            }

            var before = new
            {
                profile.Department,
                profile.ContactNumber
            };
            var after = new
            {
                Department = model.Department.Trim(),
                ContactNumber = string.IsNullOrWhiteSpace(model.ContactNumber)
                    ? null
                    : model.ContactNumber.Trim()
            };

            profile.Department = after.Department;
            profile.ContactNumber = after.ContactNumber;
            profile.ConcurrencyStamp = NewStamp();
            profile.UpdatedAt = DateTime.UtcNow;

            AddAudit(
                actorUserId,
                profile.UserId,
                ProfileUpdatedAction,
                new { Before = before, After = after });

            var failure = await TrySaveAsync(transaction);
            if (failure != null)
            {
                return failure;
            }

            await RefreshSignInClaimsAsync(profile);
            await transaction.CommitAsync();
            return BorrowerProfileResult.Success("Borrower record updated successfully.");
            });
        }

        public async Task<BorrowerProfileResult> UpdateEligibilityAsync(
            string actorUserId,
            long id,
            bool isEligible)
        {
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
            // Rebuild state on every attempt: if an earlier attempt saved IsEligible but failed
            // to commit, the cached value would otherwise make the retry report "already marked"
            // without persisting the change.
            _db.ChangeTracker.Clear();

            await using var transaction = await BeginAdministrativeWriteAsync();
            if (!await CanManageAsync(actorUserId))
            {
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Denied(DeniedMessage);
            }

            var profile = await _db.BorrowerProfiles
                .SingleOrDefaultAsync(item => item.BorrowerProfileId == id);
            if (profile == null)
            {
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Missing("Borrower record not found.");
            }

            // A raced double-toggle cannot apply twice: the second submission sees the value
            // already in place and reports that instead of writing again.
            if (profile.IsEligible == isEligible)
            {
                await transaction.CommitAsync();
                return BorrowerProfileResult.Success(
                    isEligible
                        ? "Borrower is already marked eligible."
                        : "Borrower is already marked ineligible.");
            }

            var before = profile.IsEligible;
            profile.IsEligible = isEligible;
            profile.ConcurrencyStamp = NewStamp();
            profile.UpdatedAt = DateTime.UtcNow;

            AddAudit(
                actorUserId,
                profile.UserId,
                EligibilityChangedAction,
                new
                {
                    Before = new { IsEligible = before },
                    After = new { IsEligible = isEligible }
                });

            var failure = await TrySaveAsync(transaction);
            if (failure != null)
            {
                return failure;
            }

            await transaction.CommitAsync();
            return BorrowerProfileResult.Success(
                isEligible
                    ? "Borrower marked as eligible."
                    : "Borrower marked as ineligible.");
            });
        }

        private static IQueryable<BorrowerProfileViewModel> Project(
            IQueryable<BorrowerProfile> profiles)
        {
            return profiles.Select(profile => new BorrowerProfileViewModel
            {
                BorrowerProfileId = profile.BorrowerProfileId,
                SchoolId = profile.User.UserName,
                BorrowerName = (profile.User.FirstName + " " + profile.User.LastName).Trim(),
                Department = profile.Department,
                ContactNumber = profile.ContactNumber,
                IsEligible = profile.IsEligible,
                ConcurrencyStamp = profile.ConcurrencyStamp
            });
        }

        private static bool IsCurrent(BorrowerProfile profile, string postedStamp)
        {
            return !string.IsNullOrEmpty(postedStamp) &&
                string.Equals(profile.ConcurrencyStamp, postedStamp, StringComparison.Ordinal);
        }

        private static string NewStamp() => Guid.NewGuid().ToString();

        /// <summary>
        /// Database-backed authorization for administrative writes. The controller also requires
        /// the Administrator role and the borrower.manage policy, but that policy is evaluated
        /// against the sign-in cookie, and those claims are not refreshed when a role's
        /// permissions change. Re-reading the grant here means a revoked permission - or a
        /// deactivated or demoted administrator - cannot authorize a write.
        /// This mirrors CategoryService.CanManageAsync.
        /// </summary>
        private async Task<bool> CanManageAsync(string actorUserId)
        {
            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                return false;
            }

            var actor = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == actorUserId);
            if (actor == null || !actor.IsActive)
            {
                return false;
            }

            return await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                join rolePermission in _db.RolePermissions.AsNoTracking()
                    on role.Id equals rolePermission.RoleId
                join permission in _db.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.PermissionId
                where userRole.UserId == actorUserId &&
                      role.Name == DomainValues.Roles.Administrator &&
                      permission.PermissionName == DomainValues.Permissions.BorrowerManage
                select permission.PermissionId)
                .AnyAsync();
        }

        /// <summary>
        /// Opens a serializable transaction and, on SQL Server, takes the exclusive administrative
        /// application lock. Permission, role, and account changes take the same lock, so a
        /// revocation is either visible to <see cref="CanManageAsync"/> or blocked until this
        /// write commits. Without the lock an administrator could be deactivated or demoted
        /// between the authorization check and the commit.
        /// </summary>
        private async Task<IDbContextTransaction> BeginAdministrativeWriteAsync()
        {
            var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (_db.Database.IsSqlServer())
            {
                _applicationLocks.AcquireExclusive(AdministrationLock.ResourceName);
            }

            return transaction;
        }

        /// <summary>
        /// Persists the profile mutation and its audit event together. Returns a result to hand
        /// back on failure, or null when the save succeeded.
        /// </summary>
        private async Task<BorrowerProfileResult> TrySaveAsync(IDbContextTransaction transaction)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Two administrators saved in the same instant: the token is the tie-breaker.
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Stale(StaleMessage);
            }
            catch (DbUpdateException)
            {
                // The mutation and its audit record are one unit of work; if the audit cannot be
                // written, the profile change must not survive either.
                await transaction.RollbackAsync();
                return BorrowerProfileResult.Failure(AuditFailureMessage);
            }
        }

        private void AddAudit(string actorUserId, string targetUserId, string action, object details)
        {
            _auditService.Stage(actorUserId, targetUserId, action, details);
        }

        /// <summary>
        /// Department is snapshotted into the sign-in claims and rendered in the sidebar, so an
        /// already-signed-in borrower would keep seeing the previous value. Refreshing the
        /// security stamp makes the principal rebuild (within the configured validation
        /// interval) from current claim values. This mirrors the account-edit flow in
        /// UserAdministrationService.
        /// </summary>
        private async Task RefreshSignInClaimsAsync(BorrowerProfile profile)
        {
            if (string.IsNullOrEmpty(profile.UserId))
            {
                return;
            }

            var user = await _db.Users
                .SingleOrDefaultAsync(item => item.Id == profile.UserId);
            if (user == null)
            {
                return;
            }

            await _userManager.UpdateSecurityStampAsync(user);
        }
    }
}
