using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Tests.Dashboard;
using System;
using System.Linq;

namespace ASI.Basecode.Tests.Dashboard
{
    internal static class DashboardSeed
    {
        public static ApplicationUser User(
            AsiBasecodeDBContext db,
            string code,
            bool active = true)
        {
            var user = new ApplicationUser
            {
                Id = code.ToLowerInvariant(),
                UserName = code,
                NormalizedUserName = code.ToUpperInvariant(),
                Email = $"{code.ToLowerInvariant()}@test.local",
                NormalizedEmail = $"{code.ToUpperInvariant()}@TEST.LOCAL",
                UserCode = code,
                FirstName = code,
                LastName = "Test",
                IsActive = active
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        public static BorrowerProfile Profile(
            AsiBasecodeDBContext db,
            ApplicationUser user,
            bool eligible = true)
        {
            var profile = new BorrowerProfile
            {
                UserId = user.Id,
                Department = "BSIT",
                IsEligible = eligible
            };
            db.BorrowerProfiles.Add(profile);
            db.SaveChanges();
            return profile;
        }

        public static EquipmentCategory Category(
            AsiBasecodeDBContext db,
            string name = "Laptops")
        {
            var category = db.EquipmentCategories
                .FirstOrDefault(item => item.CategoryName == name);
            if (category != null)
            {
                return category;
            }

            category = new EquipmentCategory
            {
                CategoryCode = $"CAT-{name}",
                CategoryName = name,
                IsActive = true
            };
            db.EquipmentCategories.Add(category);
            db.SaveChanges();
            return category;
        }

        public static EquipmentItem Item(
            AsiBasecodeDBContext db,
            string code,
            string status = DomainValues.EquipmentStatuses.Available,
            string categoryName = "Laptops")
        {
            var category = Category(db, categoryName);
            db.SaveChanges();

            var item = new EquipmentItem
            {
                CategoryId = category.CategoryId,
                Location = "Main Room",
                ItemCode = code,
                ItemName = code,
                ConditionStatus = DomainValues.ReturnConditions.Good,
                ItemStatus = status
            };
            db.EquipmentItems.Add(item);
            db.SaveChanges();
            return item;
        }

        public static Reservation Reservation(
            AsiBasecodeDBContext db,
            BorrowerProfile profile,
            EquipmentItem item,
            DateTime startUtc,
            DateTime endUtc,
            string status,
            DateTime? requestedAtUtc = null)
        {
            var reservation = new Reservation
            {
                BorrowerProfileId = profile.BorrowerProfileId,
                EquipmentId = item.EquipmentId,
                ReservationStart = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                ReservationEnd = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
                RequestedAt = DateTime.SpecifyKind(
                    requestedAtUtc ?? DateTime.UtcNow,
                    DateTimeKind.Utc),
                Status = status,
                Purpose = "Dashboard test"
            };
            db.Reservations.Add(reservation);
            db.SaveChanges();
            return reservation;
        }

        public static ReleaseRecord Release(
            AsiBasecodeDBContext db,
            Reservation reservation,
            DateTime atUtc)
        {
            var custodian = db.Users.FirstOrDefault(user => user.UserCode == "TEST-CUSTODIAN")
                ?? User(db, "TEST-CUSTODIAN");
            var release = new ReleaseRecord
            {
                ReservationId = reservation.ReservationId,
                ReleasedByUserId = custodian.Id,
                ActualReleaseAt = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc)
            };
            db.ReleaseRecords.Add(release);
            db.SaveChanges();
            return release;
        }

        public static void Return(
            AsiBasecodeDBContext db,
            ReleaseRecord release,
            DateTime atUtc,
            string condition = DomainValues.ReturnConditions.Good)
        {
            var custodian = db.Users.First(user => user.UserCode == "TEST-CUSTODIAN");
            db.ReturnRecords.Add(new ReturnRecord
            {
                ReleaseRecordId = release.ReleaseRecordId,
                ReceivedByUserId = custodian.Id,
                ActualReturnAt = DateTime.SpecifyKind(atUtc, DateTimeKind.Utc),
                ReturnedCondition = condition,
                ResultingItemStatus = DomainValues.EquipmentStatuses.Available
            });
            db.SaveChanges();
        }
    }
}
