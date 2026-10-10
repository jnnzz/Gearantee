using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public partial class UserAdministrationService
    {
        public async Task<UserAdministrationResult> CreateAsync(string actorUserId, CreateUserViewModel model)
        {
            await using var transaction = await BeginAdministrationTransactionAsync();
            try
            {
                if (!await CanManageAsync(actorUserId))
                    return UserAdministrationResult.AccessDenied();
                var errors = await ValidateNewAccountAsync(model, 0);
                if (errors.Count > 0)
                    return UserAdministrationResult.Failure(string.Join(" ", errors.Select(error => error.Message)));
                var result = await CreateAccountCoreAsync(actorUserId, model);
                if (result.Succeeded) await transaction.CommitAsync();
                else await RollbackCreationAsync(transaction);
                return result;
            }
            catch
            {
                await RollbackCreationAsync(transaction);
                throw;
            }
        }

        public async Task<UserImportResult> ImportAsync(string actorUserId, IReadOnlyList<UserImportRow> rows)
        {
            if (!await CanManageAsync(actorUserId)) return new UserImportResult { Forbidden = true };
            if (rows == null || rows.Count == 0 || rows.Count > UserImportCsv.MaxRows)
                return ImportFailure(0, "File", "Import between 1 and 100 accounts.");

            var errors = new List<UserImportError>();
            foreach (var row in rows)
            {
                if (row == null) return ImportFailure(0, "Row", "An account row is missing.");
                errors.AddRange(ValidateAccountFields(row.Account, row.RowNumber));
            }
            if (errors.Count > 0) return new UserImportResult { Errors = errors };

            await using var transaction = await BeginAdministrationTransactionAsync();
            var currentRow = 0;
            try
            {
                // Re-read the actor after acquiring the lock; an earlier permission check can go stale.
                _db.ChangeTracker.Clear();
                if (!await CanManageAsync(actorUserId)) return new UserImportResult { Forbidden = true };
                var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var schoolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var model = row.Account;
                    errors.AddRange(await ValidateNewAccountAsync(model, row.RowNumber));
                    if (!emails.Add(_userManager.NormalizeEmail(model.Email.Trim())))
                        errors.Add(new(row.RowNumber, "Email", "Duplicate email in this file."));
                    if (!codes.Add(_userManager.NormalizeName(model.UserCode.Trim())))
                        errors.Add(new(row.RowNumber, "UserCode", "Duplicate user code in this file."));
                    if (string.Equals(model.RoleName?.Trim(), DomainValues.Roles.Borrower, StringComparison.OrdinalIgnoreCase) &&
                        !schoolIds.Add(model.SchoolId?.Trim() ?? string.Empty))
                        errors.Add(new(row.RowNumber, "SchoolId", "Duplicate borrower school ID in this file."));
                }
                if (errors.Count > 0) return new UserImportResult { Errors = errors };

                foreach (var row in rows)
                {
                    currentRow = row.RowNumber;
                    var result = await CreateAccountCoreAsync(actorUserId, row.Account);
                    if (!result.Succeeded)
                    {
                        await RollbackCreationAsync(transaction);
                        return ImportFailure(currentRow, "Account", "Creation failed. Check identity fields, duplicates, role, and password policy. No accounts were imported.");
                    }
                }
                await transaction.CommitAsync();
                return new UserImportResult { CreatedCount = rows.Count };
            }
            catch (DbUpdateException)
            {
                await RollbackCreationAsync(transaction);
                return ImportFailure(currentRow, "Account", "A database constraint prevented creation. Check duplicate identities. No accounts were imported.");
            }
            catch
            {
                await RollbackCreationAsync(transaction);
                throw;
            }
        }

        private static List<UserImportError> ValidateAccountFields(CreateUserViewModel model, int row)
        {
            if (model == null) return new() { new(row, "Account", "An account row is missing.") };
            model.ContactNumber = CleanOptional(model.ContactNumber);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, true);
            return results.Select(result => new UserImportError(row,
                result.MemberNames.FirstOrDefault() ?? "Account", result.ErrorMessage)).ToList();
        }

        private async Task<List<UserImportError>> ValidateNewAccountAsync(CreateUserViewModel model, int row)
        {
            var errors = ValidateAccountFields(model, row);
            if (errors.Count > 0) return errors;
            var role = await _roleManager.FindByNameAsync(model.RoleName.Trim());
            if (role == null || !IsSupportedRole(role.Name))
                errors.Add(new(row, "RoleName", "Choose Borrower, Custodian, or Administrator."));
            if (await _userManager.FindByEmailAsync(model.Email.Trim()) != null)
                errors.Add(new(row, "Email", "Email is already in use."));
            if (await _db.Users.AnyAsync(user => user.UserCode == model.UserCode.Trim()))
                errors.Add(new(row, "UserCode", "User code is already in use."));
            if (string.Equals(role?.Name, DomainValues.Roles.Borrower, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.SchoolId))
                    errors.Add(new(row, "SchoolId", "Borrowers need a school ID."));
                else if (await _db.BorrowerProfiles.AnyAsync(profile => profile.SchoolId == model.SchoolId.Trim()))
                    errors.Add(new(row, "SchoolId", "School ID is already in use."));
                if (string.IsNullOrWhiteSpace(model.Department))
                    errors.Add(new(row, "Department", "Borrowers need a department."));
            }

            var candidate = new ApplicationUser { UserName = model.UserCode.Trim(), Email = model.Email.Trim() };
            foreach (var validator in _userManager.UserValidators)
            {
                if (!(await validator.ValidateAsync(_userManager, candidate)).Succeeded)
                    errors.Add(new(row, "Account", "Check the user code and email; identity validation failed."));
            }
            foreach (var validator in _userManager.PasswordValidators)
            {
                if (!(await validator.ValidateAsync(_userManager, candidate, model.Password)).Succeeded)
                    errors.Add(new(row, "Password", "Password does not meet the site's password policy."));
            }
            return errors;
        }

        private async Task RollbackCreationAsync(IDbContextTransaction transaction)
        {
            await transaction.RollbackAsync();
            // Identity saves immediately. Rolled-back tracked objects must never be saved later.
            _db.ChangeTracker.Clear();
        }

        private static UserImportResult ImportFailure(int row, string field, string message) =>
            new UserImportResult { Errors = new[] { new UserImportError(row, field, message) } };
    }
}
