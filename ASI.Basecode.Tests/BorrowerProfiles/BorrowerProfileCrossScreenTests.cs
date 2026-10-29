using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Tests.UserAdministration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.BorrowerProfiles
{
    /// <summary>
    /// The cross-screen regression the review asked for. WBS 30.00-32.00 introduced a second way
    /// to edit a borrower's department and contact number, so the account screen (Users &amp; Roles)
    /// and the borrower screen must each refuse to write over a newer save made on the other.
    /// </summary>
    public class BorrowerProfileCrossScreenTests
    {
        [Fact]
        public async Task StaleAccountForm_CannotOverwriteANewerBorrowerProfileSave()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var administration = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var borrowers = scope.ServiceProvider.GetRequiredService<IBorrowerProfileService>();

            // The borrower write path now re-reads the grant, so the actor needs it in the database.
            await GrantBorrowerManageAsync(scope);

            var borrowerUserId = await CreateBorrowerAsync(
                db, administration, environment.ActorId, "BRW-X1", "0917-100-1000");

            // An administrator opens Users & Roles -> Edit for this borrower.
            var accountForm = await administration.GetUserAsync(environment.ActorId, borrowerUserId);
            Assert.NotNull(accountForm);
            Assert.False(string.IsNullOrWhiteSpace(accountForm.BorrowerProfileConcurrencyStamp));

            // Meanwhile the contact number is corrected from the Borrower profiles screen.
            var profile = await db.BorrowerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.UserId == borrowerUserId);

            var borrowerSave = await borrowers.UpdateBorrowerProfileAsync(
                environment.ActorId,
                profile.BorrowerProfileId,
                new BorrowerProfileUpdateViewModel
                {
                    Department = "College of Computing",
                    ContactNumber = "0917-777-7777",
                    ConcurrencyStamp = profile.ConcurrencyStamp
                });
            Assert.True(borrowerSave.Succeeded, borrowerSave.Message);

            // The form opened before that save is submitted.
            accountForm.Department = "College of Arts";
            accountForm.ContactNumber = "0917-111-1111";
            var staleSubmit = await administration.UpdateAsync(environment.ActorId, accountForm);

            Assert.False(staleSubmit.Succeeded);
            Assert.True(staleSubmit.Conflict);

            // The newer values survive; the stale form did not silently win.
            var stored = await db.BorrowerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.UserId == borrowerUserId);
            Assert.Equal("0917-777-7777", stored.ContactNumber);
            Assert.Equal("College of Computing", stored.Department);
        }

        [Fact]
        public async Task StaleBorrowerForm_CannotOverwriteANewerAccountSave()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var administration = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var borrowers = scope.ServiceProvider.GetRequiredService<IBorrowerProfileService>();

            await GrantBorrowerManageAsync(scope);

            var borrowerUserId = await CreateBorrowerAsync(
                db, administration, environment.ActorId, "BRW-X2", "0917-200-2000");

            var profile = await db.BorrowerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.UserId == borrowerUserId);

            // This is the version the borrower edit form was rendered from.
            var formAsRendered = new BorrowerProfileUpdateViewModel
            {
                Department = profile.Department,
                ContactNumber = profile.ContactNumber,
                ConcurrencyStamp = profile.ConcurrencyStamp
            };

            // The account screen saves a newer contact number first.
            var accountForm = await administration.GetUserAsync(environment.ActorId, borrowerUserId);
            accountForm.ContactNumber = "0917-888-8888";
            var accountSave = await administration.UpdateAsync(environment.ActorId, accountForm);
            Assert.True(accountSave.Succeeded, accountSave.Message);

            // The stale borrower form must now be rejected rather than overwriting that save.
            formAsRendered.ContactNumber = "0917-222-2222";
            var staleSubmit = await borrowers.UpdateBorrowerProfileAsync(
                environment.ActorId, profile.BorrowerProfileId, formAsRendered);

            Assert.False(staleSubmit.Succeeded);
            Assert.True(staleSubmit.IsStale);

            var stored = await db.BorrowerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.UserId == borrowerUserId);
            Assert.Equal("0917-888-8888", stored.ContactNumber);

            // And a form loaded after the account save still saves normally - the guard is not blanket.
            var reloaded = await borrowers.GetBorrowerProfileAsync(profile.BorrowerProfileId);
            var freshSave = await borrowers.UpdateBorrowerProfileAsync(
                environment.ActorId,
                profile.BorrowerProfileId,
                new BorrowerProfileUpdateViewModel
                {
                    Department = reloaded.Department,
                    ContactNumber = "0917-333-3333",
                    ConcurrencyStamp = reloaded.ConcurrencyStamp
                });

            Assert.True(freshSave.Succeeded, freshSave.Message);
        }

        private static async Task<string> CreateBorrowerAsync(
            AsiBasecodeDBContext db,
            IUserAdministrationService administration,
            string actorId,
            string userCode,
            string contactNumber)
        {
            var created = await administration.CreateAsync(actorId, new CreateUserViewModel
            {
                UserCode = userCode,
                FirstName = "Test",
                LastName = "Borrower",
                Email = userCode.ToLowerInvariant() + "@test.local",
                RoleName = DomainValues.Roles.Borrower,
                Password = "Borrower!12345",
                ConfirmPassword = "Borrower!12345",
                SchoolId = userCode,
                Department = "College of Nursing",
                ContactNumber = contactNumber
            });
            Assert.True(created.Succeeded, created.Message);

            var user = await db.Users
                .AsNoTracking()
                .SingleAsync(item => item.UserCode == userCode);

            return user.Id;
        }

        /// <summary>
        /// AdministrationTestEnvironment grants only user_role.manage. The borrower write path
        /// re-reads borrower.manage from the database, so the acting administrator needs it here.
        /// </summary>
        private static async Task GrantBorrowerManageAsync(AsyncServiceScope scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var adminRole = await roles.FindByNameAsync(DomainValues.Roles.Administrator);
            Assert.NotNull(adminRole);

            var permission = await db.Permissions
                .FirstOrDefaultAsync(item => item.PermissionName == DomainValues.Permissions.BorrowerManage);
            if (permission == null)
            {
                permission = new Permission
                {
                    PermissionName = DomainValues.Permissions.BorrowerManage,
                    Description = "Maintain borrower profiles and eligibility."
                };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();
            }

            if (!await db.RolePermissions.AnyAsync(link =>
                    link.RoleId == adminRole.Id && link.PermissionId == permission.PermissionId))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
