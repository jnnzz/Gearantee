using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public partial class UserAdministrationService : IUserAdministrationService
    {
        private const int PageSize = 10;
        private const string AdministrationLockName = "Gearantee.UserAdministration";

        private static readonly IReadOnlyDictionary<string, string> PermissionLabels =
            new Dictionary<string, string>
            {
                [DomainValues.Permissions.EquipmentBrowse] = "Browse catalog & calendar",
                [DomainValues.Permissions.ReservationCreate] = "Submit a reservation request",
                [DomainValues.Permissions.ReservationReview] = "Approve / reject requests",
                [DomainValues.Permissions.TransactionReleaseReturn] = "Record release & return",
                [DomainValues.Permissions.EquipmentManage] = "Maintain equipment & categories",
                [DomainValues.Permissions.BorrowerManage] = "Maintain borrower profiles",
                [DomainValues.Permissions.UserRoleManage] = "Manage users & roles",
                [DomainValues.Permissions.HistoryView] = "View borrowing history report"
            };

        private readonly AsiBasecodeDBContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        public UserAdministrationService(
            AsiBasecodeDBContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<bool> CanManageAsync(string actorUserId)
        {
            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                return false;
            }

            var actor = await _db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == actorUserId);
            if (actor == null || !actor.IsActive)
            {
                return false;
            }

            var roles = await _userManager.GetRolesAsync(actor);
            if (!roles.Contains(DomainValues.Roles.Administrator))
            {
                return false;
            }

            var roleIds = await _db.Roles
                .Where(role => roles.Contains(role.Name))
                .Select(role => role.Id)
                .ToListAsync();
            var managerPermissionId = await _db.Permissions
                .Where(permission =>
                    permission.PermissionName == DomainValues.Permissions.UserRoleManage)
                .Select(permission => (long?)permission.PermissionId)
                .SingleOrDefaultAsync();

            return managerPermissionId.HasValue && await _db.RolePermissions
                .AnyAsync(link => roleIds.Contains(link.RoleId) &&
                    link.PermissionId == managerPermissionId.Value);
        }

        public async Task<UserAccountsIndexViewModel> GetAccountsAsync(
            string actorUserId,
            string search,
            string status,
            string roleId,
            int page)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            var roles = await GetRolesAsync();
            var query = _db.Users.AsNoTracking().AsQueryable();
            search = search?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(user =>
                    user.UserCode.Contains(search) ||
                    user.FirstName.Contains(search) ||
                    user.LastName.Contains(search) ||
                    (user.Email != null && user.Email.Contains(search)));
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(user => user.IsActive);
            }
            else if (string.Equals(status, "deactivated", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(user => !user.IsActive);
            }
            else
            {
                status = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(roleId) && roles.Any(role => role.Id == roleId))
            {
                query = query.Where(user => _db.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id && userRole.RoleId == roleId));
            }
            else
            {
                roleId = string.Empty;
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);
            var users = await query
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.UserCode)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(user => new UserAccountRowViewModel
                {
                    Id = user.Id,
                    UserCode = user.UserCode,
                    DisplayName = (user.FirstName + " " + user.LastName).Trim(),
                    Email = user.Email,
                    IsActive = user.IsActive
                })
                .ToListAsync();

            var pageUserIds = users.Select(user => user.Id).ToList();
            var assignments = await (
                from userRole in _db.UserRoles.AsNoTracking()
                join role in _db.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where pageUserIds.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
                .ToListAsync();

            foreach (var user in users)
            {
                user.Roles = assignments
                    .Where(assignment => assignment.UserId == user.Id)
                    .Select(assignment => assignment.Name)
                    .OrderBy(name => name)
                    .ToList();
            }

            return new UserAccountsIndexViewModel
            {
                Search = search,
                StatusFilter = status,
                RoleFilter = roleId,
                Page = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Users = users,
                Roles = roles
            };
        }

        public async Task<EditUserViewModel> GetUserAsync(
            string actorUserId,
            string userId)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            var user = await _db.Users.AsNoTracking()
                .Include(item => item.BorrowerProfile)
                .SingleOrDefaultAsync(item => item.Id == userId);
            if (user == null)
            {
                return null;
            }

            return new EditUserViewModel
            {
                Id = user.Id,
                UserCode = user.UserCode,
                ConcurrencyStamp = user.ConcurrencyStamp,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                SelectedRoles = (await _userManager.GetRolesAsync(user)).ToList(),
                AvailableRoles = await GetRolesAsync(),
                SchoolId = user.BorrowerProfile?.SchoolId,
                Department = user.BorrowerProfile?.Department,
                ContactNumber = user.BorrowerProfile?.ContactNumber,
                IsActive = user.IsActive
            };
        }

        public async Task<RolePermissionsViewModel> GetRolePermissionsAsync(
            string actorUserId)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            var roles = await GetRolesAsync();
            if (roles.Count == 0)
            {
                return null;
            }

            var roleIds = roles.Select(role => role.Id).ToList();
            var granted = await _db.RolePermissions.AsNoTracking()
                .Where(link => roleIds.Contains(link.RoleId))
                .Join(_db.Permissions.AsNoTracking(),
                    link => link.PermissionId,
                    permission => permission.PermissionId,
                    (link, permission) => new
                    {
                        permission.PermissionName,
                        link.RoleId
                    })
                .ToListAsync();
            var roleStamps = await _db.Roles.AsNoTracking()
                .Where(role => roleIds.Contains(role.Id))
                .ToDictionaryAsync(role => role.Id, role => role.ConcurrencyStamp);
            var permissionRows = await _db.Permissions.AsNoTracking()
                .OrderBy(permission => permission.PermissionId)
                .Select(permission => new
                {
                    permission.PermissionName,
                    permission.Description
                })
                .ToListAsync();

            return new RolePermissionsViewModel
            {
                Roles = roles,
                RoleStamps = roleStamps,
                Permissions = permissionRows.Select(permission => new PermissionRowViewModel
                {
                    Name = permission.PermissionName,
                    Description = PermissionLabels.TryGetValue(
                        permission.PermissionName, out var label)
                        ? label
                        : permission.Description,
                    GrantedRoleIds = granted
                        .Where(link => link.PermissionName == permission.PermissionName)
                        .Select(link => link.RoleId)
                        .ToList(),
                    IsLocked = permission.PermissionName ==
                        DomainValues.Permissions.UserRoleManage
                }).ToList()
            };
        }

        private async Task<UserAdministrationResult> CreateAccountCoreAsync(
            string actorUserId,
            CreateUserViewModel model)
        {
            var role = await _roleManager.FindByNameAsync(model.RoleName?.Trim());
            if (role == null || !IsSupportedRole(role.Name))
            {
                return UserAdministrationResult.Failure("Choose a valid system role.");
            }

            if (await _userManager.FindByEmailAsync(model.Email.Trim()) != null ||
                await _db.Users.AnyAsync(user => user.UserCode == model.UserCode.Trim()))
            {
                return UserAdministrationResult.Failure(
                    "An account with that email or user code already exists.");
            }

            if (role.Name == DomainValues.Roles.Borrower &&
                (string.IsNullOrWhiteSpace(model.SchoolId) ||
                 string.IsNullOrWhiteSpace(model.Department)))
            {
                return UserAdministrationResult.Failure(
                    "Borrower accounts need a school ID and department.");
            }

            var user = new ApplicationUser
            {
                UserName = model.UserCode.Trim(),
                UserCode = model.UserCode.Trim(),
                Email = model.Email.Trim(),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                var created = await _userManager.CreateAsync(user, model.Password);
                if (!created.Succeeded)
                {
                    return UserAdministrationResult.Failure(IdentityErrors(created));
                }

                var addedRole = await _userManager.AddToRoleAsync(user, role.Name);
                if (!addedRole.Succeeded)
                {
                    return IdentityFailure(addedRole);
                }

                if (role.Name == DomainValues.Roles.Borrower)
                {
                    var schoolId = string.IsNullOrWhiteSpace(model.SchoolId)
                        ? user.UserCode
                        : model.SchoolId.Trim();
                    if (await _db.BorrowerProfiles.AnyAsync(profile =>
                        profile.SchoolId == schoolId))
                    {
                        return UserAdministrationResult.Failure(
                            "A borrower profile already uses that school ID.");
                    }

                    _db.BorrowerProfiles.Add(new BorrowerProfile
                    {
                        UserId = user.Id,
                        SchoolId = schoolId,
                        Department = model.Department.Trim(),
                        ContactNumber = CleanOptional(model.ContactNumber),
                        IsEligible = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                AddAudit(actorUserId, user.Id, null, "AccountCreated", new
                {
                    user.UserCode,
                    user.Email,
                    role.Name
                });
                await _db.SaveChangesAsync();
                return UserAdministrationResult.Success("Account created.");
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return UserAdministrationResult.Failure(
                    "The email, user code, or school ID is already in use.");
            }
        }

        public async Task<UserAdministrationResult> UpdateAsync(
            string actorUserId,
            EditUserViewModel model)
        {
            await using var transaction = await BeginAdministrationTransactionAsync();
            if (!await CanManageAsync(actorUserId))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.AccessDenied();
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Missing();
            }

            if (!string.Equals(user.ConcurrencyStamp, model.ConcurrencyStamp,
                    StringComparison.Ordinal))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Stale(
                    "This account changed after you opened it. Reload the page and try again.");
            }

            var selectedRoles = (model.SelectedRoles ?? new List<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedRoles.Count == 0 || selectedRoles.Any(role => !IsSupportedRole(role)))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "Assign at least one valid role to the account.");
            }

            var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
            if (string.Equals(actorUserId, user.Id, StringComparison.Ordinal) &&
                currentRoles.Contains(DomainValues.Roles.Administrator) &&
                !selectedRoles.Contains(DomainValues.Roles.Administrator,
                    StringComparer.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "You cannot remove your own Administrator role.");
            }

            var removesAdministrator = currentRoles.Contains(
                    DomainValues.Roles.Administrator) &&
                !selectedRoles.Contains(DomainValues.Roles.Administrator,
                    StringComparer.OrdinalIgnoreCase);
            if (user.IsActive && removesAdministrator && await CountActiveAdministratorsAsync() <= 1)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "The last active administrator must retain the Administrator role.");
            }

            var email = model.Email.Trim();
            var existingEmailUser = await _userManager.FindByEmailAsync(email);
            if (existingEmailUser != null && existingEmailUser.Id != user.Id)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "An account with that email already exists.");
            }

            var needsBorrowerProfile = selectedRoles.Contains(
                DomainValues.Roles.Borrower, StringComparer.OrdinalIgnoreCase);
            if (needsBorrowerProfile &&
                (string.IsNullOrWhiteSpace(model.SchoolId) ||
                 string.IsNullOrWhiteSpace(model.Department)))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "Accounts with the Borrower role need a school ID and department.");
            }

            var profile = await _db.BorrowerProfiles
                .SingleOrDefaultAsync(item => item.UserId == user.Id);
            if (needsBorrowerProfile)
            {
                var schoolId = model.SchoolId.Trim();
                if (await _db.BorrowerProfiles.AnyAsync(item =>
                    item.UserId != user.Id && item.SchoolId == schoolId))
                {
                    await transaction.RollbackAsync();
                    return UserAdministrationResult.Failure(
                        "A borrower profile already uses that school ID.");
                }
            }

            var changed = new Dictionary<string, object>();
            var before = new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                Roles = currentRoles.OrderBy(role => role).ToArray(),
                SchoolId = profile?.SchoolId,
                Department = profile?.Department,
                ContactNumber = profile?.ContactNumber
            };

            try
            {
                user.FirstName = model.FirstName.Trim();
                user.LastName = model.LastName.Trim();
                user.UpdatedAt = DateTime.UtcNow;
                var updateResult = email == user.Email
                    ? await _userManager.UpdateAsync(user)
                    : await _userManager.SetEmailAsync(user, email);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return IdentityFailure(updateResult);
                }

                foreach (var role in currentRoles.Except(
                    selectedRoles, StringComparer.OrdinalIgnoreCase))
                {
                    var result = await _userManager.RemoveFromRoleAsync(user, role);
                    if (!result.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return IdentityFailure(result);
                    }
                }

                foreach (var role in selectedRoles.Except(
                    currentRoles, StringComparer.OrdinalIgnoreCase))
                {
                    var result = await _userManager.AddToRoleAsync(user, role);
                    if (!result.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return IdentityFailure(result);
                    }
                }

                if (needsBorrowerProfile)
                {
                    if (profile == null)
                    {
                        profile = new BorrowerProfile
                        {
                            UserId = user.Id,
                            SchoolId = model.SchoolId.Trim(),
                            Department = model.Department.Trim(),
                            IsEligible = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.BorrowerProfiles.Add(profile);
                    }
                    else
                    {
                        profile.SchoolId = model.SchoolId.Trim();
                        profile.Department = model.Department.Trim();
                    }

                    profile.ContactNumber = CleanOptional(model.ContactNumber);
                    profile.UpdatedAt = DateTime.UtcNow;
                }

                await _userManager.UpdateSecurityStampAsync(user);
                changed["FirstName"] = user.FirstName;
                changed["LastName"] = user.LastName;
                changed["Email"] = user.Email;
                changed["Roles"] = selectedRoles.OrderBy(role => role).ToArray();
                changed["SchoolId"] = needsBorrowerProfile ? model.SchoolId.Trim() : profile?.SchoolId;
                changed["Department"] = needsBorrowerProfile ? model.Department.Trim() : profile?.Department;
                changed["ContactNumber"] = needsBorrowerProfile ? CleanOptional(model.ContactNumber) : profile?.ContactNumber;

                AddAudit(actorUserId, user.Id, null, "AccountUpdated", new
                {
                    Before = before,
                    After = changed
                });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return UserAdministrationResult.Success("Account updated.");
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "The email, user code, or school ID is already in use.");
            }
        }

        public async Task<UserAdministrationResult> SetActiveAsync(
            string actorUserId,
            SetAccountActiveViewModel model)
        {
            await using var transaction = await BeginAdministrationTransactionAsync();
            if (!await CanManageAsync(actorUserId))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.AccessDenied();
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Missing();
            }

            if (!string.Equals(user.ConcurrencyStamp, model.ConcurrencyStamp,
                    StringComparison.Ordinal))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Stale(
                    "This account changed after you opened it. Reload the page and try again.");
            }

            if (user.IsActive == model.IsActive)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Success(
                    user.IsActive ? "Account is already active." : "Account is already deactivated.");
            }

            if (!model.IsActive && user.Id == actorUserId)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "You cannot deactivate your own account.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!model.IsActive && roles.Contains(DomainValues.Roles.Administrator) &&
                await CountActiveAdministratorsAsync() <= 1)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "The last active administrator cannot be deactivated.");
            }

            var previous = user.IsActive;
            user.IsActive = model.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                return IdentityFailure(result);
            }

            await _userManager.UpdateSecurityStampAsync(user);
            AddAudit(actorUserId, user.Id, null,
                model.IsActive ? "AccountReactivated" : "AccountDeactivated",
                new { Before = new { IsActive = previous }, After = new { IsActive = model.IsActive } });
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return UserAdministrationResult.Success(
                model.IsActive ? "Account reactivated." : "Account deactivated.");
        }

        public async Task<UserAdministrationResult> UpdatePermissionsAsync(
            string actorUserId,
            UpdateRolePermissionsViewModel model)
        {
            await using var transaction = await BeginAdministrationTransactionAsync();
            if (!await CanManageAsync(actorUserId))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.AccessDenied();
            }

            var known = PermissionLabels.Keys.ToHashSet(StringComparer.Ordinal);
            var roles = await GetRolesAsync();
            var roleIds = roles.Select(role => role.Id).ToHashSet(StringComparer.Ordinal);
            var roleStamps = model.RoleStamps ?? new Dictionary<string, string>();
            if (roles.Count == 0 || roleStamps.Count != roles.Count ||
                roleIds.Any(id => !roleStamps.ContainsKey(id)))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Stale(
                    "The role list changed. Reload the page and review the current matrix.");
            }

            var roleEntities = new List<IdentityRole>();
            foreach (var roleOption in roles)
            {
                var role = await _roleManager.FindByIdAsync(roleOption.Id);
                if (role == null)
                {
                    await transaction.RollbackAsync();
                    return UserAdministrationResult.Missing();
                }

                if (!string.Equals(role.ConcurrencyStamp, roleStamps[role.Id],
                        StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync();
                    return UserAdministrationResult.Stale(
                        "Role permissions changed after the page was opened. Reload and review the current matrix.");
                }

                roleEntities.Add(role);
            }

            var assignments = (model.Assignments ?? new List<PermissionSelectionViewModel>())
                .GroupBy(item => item.PermissionName, StringComparer.Ordinal)
                .ToList();
            if (assignments.Count != known.Count ||
                assignments.Any(item => item.Count() != 1 || !known.Contains(item.Key)))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure("Submit one selection for every known capability.");
            }

            var requestedByPermission = assignments.ToDictionary(
                group => group.Key,
                group => (group.First().RoleIds ?? new List<string>())
                    .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
            if (requestedByPermission.Values.Any(ids => ids.Any(id => !roleIds.Contains(id))))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure("The submitted matrix contains an unknown role.");
            }

            var administratorRole = roles.SingleOrDefault(role =>
                role.Name == DomainValues.Roles.Administrator);
            if (administratorRole == null || !requestedByPermission[
                    DomainValues.Permissions.UserRoleManage].Contains(administratorRole.Id) ||
                requestedByPermission[DomainValues.Permissions.UserRoleManage]
                    .Any(id => id != administratorRole.Id))
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "User and role management must remain assigned only to the Administrator role.");
            }

            var permissionIds = await _db.Permissions.AsNoTracking()
                .Where(permission => known.Contains(permission.PermissionName))
                .ToDictionaryAsync(permission => permission.PermissionName,
                    permission => permission.PermissionId);
            if (permissionIds.Count != known.Count)
            {
                await transaction.RollbackAsync();
                return UserAdministrationResult.Failure(
                    "The permission catalogue is incomplete. Run the application seeder and retry.");
            }
            var permissionNamesById = permissionIds.ToDictionary(
                item => item.Value,
                item => item.Key);

            foreach (var role in roleEntities)
            {
                var requestedForRole = requestedByPermission
                    .Where(item => item.Value.Contains(role.Id))
                    .Select(item => item.Key)
                    .ToHashSet(StringComparer.Ordinal);
                var existingLinks = await _db.RolePermissions
                    .Where(link => link.RoleId == role.Id)
                    .ToListAsync();
                var existingNames = await _db.RolePermissions
                    .Where(link => link.RoleId == role.Id)
                    .Join(_db.Permissions,
                        link => link.PermissionId,
                        permission => permission.PermissionId,
                        (link, permission) => permission.PermissionName)
                    .ToListAsync();
                var removed = existingLinks
                    .Where(link => !permissionNamesById.TryGetValue(
                            link.PermissionId, out var permissionName) ||
                        !requestedForRole.Contains(permissionName))
                    .ToList();
                _db.RolePermissions.RemoveRange(removed);

                foreach (var permissionName in requestedForRole.Except(existingNames))
                {
                    _db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionIds[permissionName]
                    });
                }

                if (!requestedForRole.SetEquals(existingNames))
                {
                    role.ConcurrencyStamp = Guid.NewGuid().ToString();
                    var roleUpdate = await _roleManager.UpdateAsync(role);
                    if (!roleUpdate.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return IdentityFailure(roleUpdate);
                    }

                    AddAudit(actorUserId, null, role.Id, "RolePermissionsUpdated", new
                    {
                        Role = role.Name,
                        Before = existingNames.OrderBy(name => name).ToArray(),
                        After = requestedForRole.OrderBy(name => name).ToArray()
                    });
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return UserAdministrationResult.Success("Role permissions saved.");
        }

        private async Task<IDbContextTransaction> BeginAdministrationTransactionAsync()
        {
            var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);
            if (_db.Database.IsSqlServer())
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $@"DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = {AdministrationLockName},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 15000;
IF @lockResult < 0 THROW 51000, 'Could not acquire the user administration lock.', 1;");
            }

            return transaction;
        }

        private async Task<int> CountActiveAdministratorsAsync()
        {
            return await (
                from user in _db.Users
                join userRole in _db.UserRoles on user.Id equals userRole.UserId
                join role in _db.Roles on userRole.RoleId equals role.Id
                where user.IsActive && role.Name == DomainValues.Roles.Administrator
                select user.Id)
                .Distinct()
                .CountAsync();
        }

        private async Task<List<RoleOptionViewModel>> GetRolesAsync()
        {
            var supportedRoles = new[]
            {
                DomainValues.Roles.Borrower,
                DomainValues.Roles.Custodian,
                DomainValues.Roles.Administrator
            };

            return await _db.Roles.AsNoTracking()
                .Where(role => supportedRoles.Contains(role.Name))
                .OrderBy(role => role.Name)
                .Select(role => new RoleOptionViewModel
                {
                    Id = role.Id,
                    Name = role.Name
                })
                .ToListAsync();
        }

        private void AddAudit(
            string actorUserId,
            string targetUserId,
            string targetRoleId,
            string action,
            object details)
        {
            _db.AdministrationAuditEvents.Add(new AdministrationAuditEvent
            {
                ActorUserId = actorUserId,
                TargetUserId = targetUserId,
                TargetRoleId = targetRoleId,
                Action = action,
                DetailsJson = JsonSerializer.Serialize(details),
                OccurredAt = DateTime.UtcNow
            });
        }

        private static bool IsSupportedRole(string role) =>
            string.Equals(role, DomainValues.Roles.Borrower, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, DomainValues.Roles.Custodian, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, DomainValues.Roles.Administrator, StringComparison.OrdinalIgnoreCase);

        private static string CleanOptional(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool IsUniqueViolation(DbUpdateException exception)
        {
            var sqlException = exception.GetBaseException() as SqlException;
            return sqlException != null &&
                (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

        private static UserAdministrationResult IdentityFailure(IdentityResult result)
        {
            if (result.Errors.Any(error => error.Code == "ConcurrencyFailure"))
            {
                return UserAdministrationResult.Stale(
                    "This record changed while you were saving. Reload it and try again.");
            }

            return UserAdministrationResult.Failure(IdentityErrors(result));
        }

        private static string IdentityErrors(IdentityResult result) =>
            string.Join(" ", result.Errors.Select(error => error.Description));
    }
}
