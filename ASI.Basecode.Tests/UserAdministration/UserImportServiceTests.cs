using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    public class UserImportServiceTests
    {
        internal static CreateUserViewModel Account(string code, string role = "Custodian") => new()
        {
            UserCode = code, FirstName = "Ana", LastName = "Test", Email = code + "@test.local",
            RoleName = role, Password = "Private!123456", ConfirmPassword = "Private!123456",
            SchoolId = role == "Borrower" ? code : null, Department = role == "Borrower" ? "Science" : null
        };

        [Fact]
        public async Task ImportCreatesMixedRolesAndSafeAudits_RejectsReupload()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var rows = new[] { new UserImportRow(2, Account("0001", "Borrower")), new UserImportRow(3, Account("STAFF")) };
            var result = await service.ImportAsync(environment.ActorId, rows);
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.CreatedCount);
            Assert.Equal(3, await db.Users.CountAsync());
            Assert.Equal(3, await db.UserRoles.CountAsync());
            var profile = await db.BorrowerProfiles.SingleAsync();
            Assert.Equal("0001", profile.SchoolId);
            Assert.False(profile.IsEligible);
            var audits = await db.AdministrationAuditEvents.ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, audit => Assert.Equal(environment.ActorId, audit.ActorUserId));
            Assert.DoesNotContain(rows[0].Account.Password, JsonSerializer.Serialize(audits));
            Assert.NotEmpty((await service.ImportAsync(environment.ActorId, rows)).Errors);
            Assert.Equal(3, await db.Users.CountAsync());
        }

        [Theory]
        [InlineData("email")]
        [InlineData("password")]
        [InlineData("role")]
        [InlineData("borrower")]
        [InlineData("duplicate-email")]
        [InlineData("duplicate-code")]
        [InlineData("duplicate-school")]
        [InlineData("existing-email")]
        [InlineData("existing-code")]
        public async Task ValidationErrorsCreateNothing(string problem)
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var first = Account("A1", "Borrower");
            var second = Account("A2", "Borrower");
            switch (problem)
            {
                case "email": second.Email = "invalid"; break;
                case "password": second.Password = second.ConfirmPassword = "weak"; break;
                case "role": second.RoleName = "Superuser"; break;
                case "borrower": second.Department = ""; break;
                case "duplicate-email": second.Email = first.Email.ToUpperInvariant(); break;
                case "duplicate-code": second.UserCode = first.UserCode.ToLowerInvariant(); break;
                case "duplicate-school": second.SchoolId = first.SchoolId.ToLowerInvariant(); break;
                case "existing-email": second.Email = "ADMIN@TEST.LOCAL"; break;
                case "existing-code": second.UserCode = "ADMIN-TEST"; break;
            }
            var result = await service.ImportAsync(environment.ActorId, new[] { new UserImportRow(2, first), new UserImportRow(3, second) });
            Assert.NotEmpty(result.Errors);
            Assert.Equal(0, result.CreatedCount);
            Assert.DoesNotContain(first.Password, JsonSerializer.Serialize(result.Errors));
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Empty(await db.BorrowerProfiles.ToListAsync());
            Assert.Empty(await db.AdministrationAuditEvents.ToListAsync());
        }

        [Theory]
        [InlineData("inactive")]
        [InlineData("permission")]
        [InlineData("role")]
        [InlineData("anonymous")]
        public async Task ImportRequiresCurrentActiveAdministrator(string restriction)
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var actor = await users.FindByIdAsync(environment.ActorId);
            if (restriction == "inactive") { actor.IsActive = false; await users.UpdateAsync(actor); }
            if (restriction == "permission") { db.RolePermissions.RemoveRange(db.RolePermissions); await db.SaveChangesAsync(); }
            if (restriction == "role") await users.RemoveFromRoleAsync(actor, "Administrator");
            var result = await scope.ServiceProvider.GetRequiredService<IUserAdministrationService>()
                .ImportAsync(restriction == "anonymous" ? null : actor.Id, new[] { new UserImportRow(2, Account("NEW")) });
            Assert.True(result.Forbidden);
            Assert.Equal(1, await db.Users.CountAsync());
        }

        [Fact]
        public Task LaterFailureRollsBackEarlierAccounts() => CheckRollback(false);

        [Fact]
        public async Task ExistingSchoolIdRejectsBatch_RealTransitionAuditsOnce_StaleRepeatIsRejected()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.True((await service.CreateAsync(environment.ActorId, Account("SCHOOL", "Borrower"))).Succeeded);
            var duplicate = Account("DIFFERENT", "Borrower");
            duplicate.SchoolId = "SCHOOL";
            Assert.NotEmpty((await service.ImportAsync(environment.ActorId,
                new[] { new UserImportRow(2, duplicate) })).Errors);
            Assert.Equal(2, await db.Users.CountAsync());
            var target = await db.Users.SingleAsync(user => user.UserCode == "SCHOOL");
            var command = new SetAccountActiveViewModel
                { UserId = target.Id, ConcurrencyStamp = target.ConcurrencyStamp, IsActive = false };
            Assert.True((await service.SetActiveAsync(environment.ActorId, command)).Succeeded);
            Assert.True((await service.SetActiveAsync(environment.ActorId, command)).Conflict);
            Assert.Equal(1, await db.AdministrationAuditEvents.CountAsync(audit => audit.Action == "AccountDeactivated"));
        }

        [Fact]
        public async Task AuthorizationDoesNotTrustAnAlreadyTrackedActiveActor()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var original = environment.Provider.CreateAsyncScope();
            var db = original.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.True((await db.Users.SingleAsync()).IsActive);
            await using (var other = environment.Provider.CreateAsyncScope())
            {
                var otherDb = other.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                (await otherDb.Users.SingleAsync()).IsActive = false;
                await otherDb.SaveChangesAsync();
            }
            Assert.False(await original.ServiceProvider.GetRequiredService<IUserAdministrationService>()
                .CanManageAsync(environment.ActorId));
        }

        [SqlServerFact]
        public Task SqlServerLaterFailureRollsBackEarlierAccounts() => CheckRollback(true);

        private static async Task CheckRollback(bool sqlServer)
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync(sqlServer);
            await using var scope = environment.Provider.CreateAsyncScope();
            environment.Fault.Enabled = true;
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            var result = await service.ImportAsync(environment.ActorId, new[]
            {
                new UserImportRow(2, Account("FIRST", "Borrower")), new UserImportRow(3, Account("SECOND", "Borrower"))
            });
            Assert.Equal(2, environment.Fault.AuditAttempts);
            Assert.Equal(0, result.CreatedCount);
            Assert.NotEmpty(result.Errors);
            await db.SaveChangesAsync(); // A later save must not resurrect rolled-back tracked entries.
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await db.UserRoles.CountAsync());
            Assert.Empty(await db.BorrowerProfiles.ToListAsync());
            Assert.Empty(await db.AdministrationAuditEvents.ToListAsync());
        }

        [SqlServerFact]
        public async Task SqlServerConcurrentImportsSerializeAndUniqueEmailIndexRemainsEnforced()
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync(true);
            async Task<UserImportResult> Import()
            {
                await using var scope = environment.Provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IUserAdministrationService>()
                    .ImportAsync(environment.ActorId, new[] { new UserImportRow(2, Account("SHARED")) });
            }
            var results = await Task.WhenAll(Import(), Import());
            Assert.Equal(1, results.Sum(result => result.CreatedCount));
            Assert.Single(results, result => result.Errors.Count > 0);
            await using var scope = environment.Provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.Single(await db.AdministrationAuditEvents.ToListAsync());
            var original = await db.Users.SingleAsync(user => user.UserCode == "SHARED");
            db.Users.Add(new ApplicationUser
            {
                UserName = "DIFFERENT", NormalizedUserName = "DIFFERENT", UserCode = "DIFFERENT",
                FirstName = "Test", LastName = "Test", Email = original.Email, NormalizedEmail = original.NormalizedEmail,
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnchangedActivationDoesNotWriteOrAudit(bool active)
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            Assert.True((await service.CreateAsync(environment.ActorId, Account("TARGET"))).Succeeded);
            var target = await users.FindByNameAsync("TARGET");
            target.IsActive = active;
            await users.UpdateAsync(target);
            var stamp = target.SecurityStamp;
            var concurrency = target.ConcurrencyStamp;
            var updatedAt = target.UpdatedAt;
            var audits = await db.AdministrationAuditEvents.CountAsync();
            var result = await service.SetActiveAsync(environment.ActorId, new SetAccountActiveViewModel
                { UserId = target.Id, ConcurrencyStamp = concurrency, IsActive = active });
            Assert.True(result.Succeeded);
            await db.Entry(target).ReloadAsync();
            Assert.Equal(stamp, target.SecurityStamp);
            Assert.Equal(concurrency, target.ConcurrencyStamp);
            Assert.Equal(updatedAt, target.UpdatedAt);
            Assert.Equal(audits, await db.AdministrationAuditEvents.CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OtherAdminCanBeDemotedWhetherActiveOrInactive(bool active)
        {
            await using var environment = await AdministrationTestEnvironment.CreateAsync();
            await using var scope = environment.Provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
            Assert.True((await service.CreateAsync(environment.ActorId, Account("OTHER", "Administrator"))).Succeeded);
            var other = await users.FindByNameAsync("OTHER");
            other.IsActive = active;
            await users.UpdateAsync(other);
            var oldSecurityStamp = other.SecurityStamp;
            var model = await service.GetUserAsync(environment.ActorId, other.Id);
            model.SelectedRoles = new List<string> { "Custodian" };
            Assert.True((await service.UpdateAsync(environment.ActorId, model)).Succeeded);
            Assert.False(await users.IsInRoleAsync(other, "Administrator"));
            Assert.Equal(active, other.IsActive);
            Assert.NotEqual(oldSecurityStamp, other.SecurityStamp);
            var actorModel = await service.GetUserAsync(environment.ActorId, environment.ActorId);
            actorModel.SelectedRoles = new List<string> { "Custodian" };
            Assert.False((await service.UpdateAsync(environment.ActorId, actorModel)).Succeeded);
            Assert.True(await users.IsInRoleAsync(await users.FindByIdAsync(environment.ActorId), "Administrator"));
        }
    }
}
