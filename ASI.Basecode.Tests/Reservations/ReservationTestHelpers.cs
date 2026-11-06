using ASI.Basecode.Data;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using System;

namespace ASI.Basecode.Tests.Reservations
{
    internal static class ReservationTestHelpers
    {
        public static CatalogService Catalog(AsiBasecodeDBContext db, DateTime nowUtc)
        {
            var unitOfWork = new UnitOfWork(db);
            return new CatalogService(
                new EquipmentItemRepository(unitOfWork),
                new ReservationRepository(unitOfWork),
                new FixedTimeProvider(nowUtc));
        }

        public static ReservationService ReservationsFor(AsiBasecodeDBContext db, DateTime nowUtc)
        {
            var unitOfWork = new UnitOfWork(db);
            return new ReservationService(
                unitOfWork,
                new ReservationRepository(unitOfWork),
                new EquipmentItemRepository(unitOfWork),
                new BorrowerProfileRepository(unitOfWork),
                new AuditService(unitOfWork),
                new FixedTimeProvider(nowUtc));
        }

        public static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        public sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime nowUtc)
            {
                _now = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));
            }

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
