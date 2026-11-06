using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;

namespace ASI.Basecode.Tests.Returns
{
    internal static class ReturnTestHelpers
    {
        public static ReturnRecordService ReturnsFor(AsiBasecodeDBContext db, DateTime nowUtc)
        {
            var unitOfWork = new UnitOfWork(db);
            return new ReturnRecordService(
                unitOfWork,
                new ReturnRecordRepository(unitOfWork),
                new ReservationRepository(unitOfWork),
                new EquipmentItemRepository(unitOfWork),
                new FixedTimeProvider(nowUtc));
        }

        // Staff user in a role, optionally granted transaction.release_return
        public static ApplicationUser Staff(AsiBasecodeDBContext db, string code, string roleName, bool granted = true, bool active = true)
        {
            var user = DashboardSeed.User(db, code, active);
            var role = db.Roles.FirstOrDefault(item => item.Name == roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName) { NormalizedName = roleName.ToUpperInvariant() };
                db.Roles.Add(role);
            }

            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            db.SaveChanges();

            if (granted && !db.RolePermissions.Any(link => link.RoleId == role.Id))
            {
                var permission = db.Permissions.FirstOrDefault(item => item.PermissionName == DomainValues.Permissions.TransactionReleaseReturn);
                if (permission == null)
                {
                    permission = new Permission
                    {
                        PermissionName = DomainValues.Permissions.TransactionReleaseReturn,
                        Description = "Record release & return"
                    };
                    db.Permissions.Add(permission);
                    db.SaveChanges();
                }

                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.PermissionId });
                db.SaveChanges();
            }

            return user;
        }

        // Approved reservation released at releasedUtc, item marked Borrowed
        public static ReleaseRecord Loan(AsiBasecodeDBContext db, string itemCode, DateTime startUtc, DateTime dueUtc, DateTime releasedUtc)
        {
            var borrower = DashboardSeed.User(db, $"BRW-{itemCode}");
            var profile = DashboardSeed.Profile(db, borrower);
            var item = DashboardSeed.Item(db, itemCode, DomainValues.EquipmentStatuses.Borrowed);
            var reservation = DashboardSeed.Reservation(db, profile, item, startUtc, dueUtc, DomainValues.ReservationStatuses.Approved);
            return DashboardSeed.Release(db, reservation, releasedUtc);
        }
    }
}
