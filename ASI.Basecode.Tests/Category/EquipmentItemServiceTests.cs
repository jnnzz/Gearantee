using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class EquipmentItemServiceTests
    {
        [Fact]
        public async Task CreateAsync_WithCatalogImage_PersistsImageUrl()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.ImageUrl = "https://assets.example.test/projector.jpg";

            var result = await service.CreateAsync(admin.Id, model);

            Assert.True(result.Succeeded, result.Message);
            var saved = await db.EquipmentItems.AsNoTracking()
                .SingleAsync(item => item.EquipmentId == result.EquipmentId);
            Assert.Equal(model.ImageUrl, saved.ImageUrl);
        }

        [Fact]
        public async Task CreateAsync_WithoutCatalogImage_RemainsValid()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            var result = await service.CreateAsync(admin.Id, NewItemModel(category.CategoryId));

            Assert.True(result.Succeeded, result.Message);
            var saved = await db.EquipmentItems.AsNoTracking()
                .SingleAsync(item => item.EquipmentId == result.EquipmentId);
            Assert.Null(saved.ImageUrl);
        }

        [Fact]
        public async Task CreateAsync_WithInactiveCategory_IsRejected()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db, isActive: false);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            var result = await service.CreateAsync(admin.Id, NewItemModel(category.CategoryId));

            Assert.False(result.Succeeded);
            Assert.Contains("active equipment category", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await db.EquipmentItems.ToListAsync());
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateSerialNumber_IsRejected()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            await CreateEquipmentAsync(db, category.CategoryId, serialNumber: "SERIAL-001");
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.SerialNumber = "serial-001";

            var result = await service.CreateAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("serial number", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateItemCode_IsRejected()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var existing = await CreateEquipmentAsync(db, category.CategoryId);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.ItemCode = existing.ItemCode.ToLowerInvariant();

            var result = await service.CreateAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("code", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("//evil.example.test/image.jpg")]
        [InlineData("http://assets.example.test/image.jpg")]
        public async Task CreateAsync_WithUnsafeImageLocation_ReturnsFailure(string imageUrl)
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.ImageUrl = imageUrl;

            var result = await service.CreateAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("HTTPS URL", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await db.EquipmentItems.ToListAsync());
        }

        [Fact]
        public async Task UpdateAsync_ChangesAndClearsCatalogImage()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId, "/img/old.jpg");
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.EquipmentId = item.EquipmentId;
            model.ItemCode = item.ItemCode;
            model.ImageUrl = "/img/new.jpg";

            var update = await service.UpdateAsync(admin.Id, model);
            Assert.True(update.Succeeded, update.Message);
            Assert.Equal("/img/new.jpg", (await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).ImageUrl);

            model.ImageUrl = " ";
            var clear = await service.UpdateAsync(admin.Id, model);
            Assert.True(clear.Succeeded, clear.Message);
            Assert.Null((await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).ImageUrl);
        }

        [Fact]
        public async Task UpdateAsync_WithUnsafeImageUrl_DoesNotChangeExistingImage()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId, "/img/current.jpg");
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.EquipmentId = item.EquipmentId;
            model.ItemCode = item.ItemCode;
            model.ImageUrl = "javascript:alert(1)";

            var result = await service.UpdateAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Equal("/img/current.jpg", (await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).ImageUrl);
        }

        [Fact]
        public async Task UpdateAsync_WithPendingReservation_DoesNotChangeItem()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId);
            await AddReservationAsync(scope, item.EquipmentId,
                DomainValues.ReservationStatuses.Pending, DateTime.UtcNow.AddDays(1));
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.EquipmentId = item.EquipmentId;
            model.ItemCode = item.ItemCode;
            model.ItemStatus = DomainValues.EquipmentStatuses.UnderMaintenance;

            var result = await service.UpdateAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("pending reservation", result.Message, StringComparison.OrdinalIgnoreCase);
            var unchanged = await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId);
            Assert.Equal(DomainValues.EquipmentStatuses.Available, unchanged.ItemStatus);
        }

        [Fact]
        public async Task UpdateAsync_WithExpiredPendingReservation_AllowsInventoryChange()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId);
            await AddReservationAsync(scope, item.EquipmentId,
                DomainValues.ReservationStatuses.Pending, DateTime.UtcNow.AddDays(-1));
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();
            var model = NewItemModel(category.CategoryId);
            model.EquipmentId = item.EquipmentId;
            model.ItemCode = item.ItemCode;
            model.ItemStatus = DomainValues.EquipmentStatuses.UnderMaintenance;

            var result = await service.UpdateAsync(admin.Id, model);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(DomainValues.EquipmentStatuses.UnderMaintenance,
                (await db.EquipmentItems.AsNoTracking()
                    .SingleAsync(row => row.EquipmentId == item.EquipmentId)).ItemStatus);
        }

        [Fact]
        public async Task SetArchivedAsync_WithApprovedUnreturnedReservation_RejectsArchive()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId);
            await AddReservationAsync(scope, item.EquipmentId,
                DomainValues.ReservationStatuses.Approved, DateTime.UtcNow.AddDays(1));
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            var result = await service.SetArchivedAsync(admin.Id, item.EquipmentId, true);

            Assert.False(result.Succeeded);
            Assert.Contains("active loan", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False((await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).IsArchived);
        }

        [Fact]
        public async Task SetArchivedAsync_ArchivesAndRestoresWithoutDeletingHistory()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            var archived = await service.SetArchivedAsync(admin.Id, item.EquipmentId, true);
            var restored = await service.SetArchivedAsync(admin.Id, item.EquipmentId, false);

            Assert.True(archived.Succeeded, archived.Message);
            Assert.True(restored.Succeeded, restored.Message);
            Assert.NotNull(await db.EquipmentItems.AsNoTracking()
                .SingleOrDefaultAsync(row => row.EquipmentId == item.EquipmentId));
            Assert.False((await db.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).IsArchived);
        }

        [Fact]
        public async Task SetArchivedAsync_AfterUncommittedSaveOnSharedContext_PersistsThroughFreshContext()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var admin = await SetupAuthorizedAdminAsync(scope);
            var category = await CreateCategoryAsync(db);
            var item = await CreateEquipmentAsync(db, category.CategoryId);
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            // Simulate an attempt that saved IsArchived=true but never committed. The shared
            // DbContext keeps that uncommitted value tracked, exactly like a retried transaction.
            await using (var failedAttempt = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable))
            {
                var tracked = await db.EquipmentItems
                    .SingleAsync(row => row.EquipmentId == item.EquipmentId);
                tracked.IsArchived = true;
                await db.SaveChangesAsync();
                await failedAttempt.RollbackAsync();
            }

            Assert.Contains(
                db.ChangeTracker.Entries<EquipmentItem>(),
                entry => entry.Entity.IsArchived);

            var result = await service.SetArchivedAsync(admin.Id, item.EquipmentId, true);

            Assert.True(result.Succeeded, result.Message);
            // The retry must rebuild state and write the value; it must not trust the stale
            // tracked IsArchived=true and report success without touching the database.
            using var verify = database.Create();
            Assert.True((await verify.EquipmentItems.AsNoTracking()
                .SingleAsync(row => row.EquipmentId == item.EquipmentId)).IsArchived);
        }

        [Fact]
        public async Task CreateAsync_WithoutEquipmentManagePermission_IsForbidden()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await AddUserAsync(users, "BORROWER-01");
            var service = scope.ServiceProvider.GetRequiredService<IEquipmentItemService>();

            var result = await service.CreateAsync(user.Id, NewItemModel(1));

            Assert.False(result.Succeeded);
            Assert.True(result.Forbidden);
        }

        private static ServiceProvider CreateProvider(SqliteDb database)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            services.AddDataProtection();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(database.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IEquipmentItemService, EquipmentItemService>();
            return services.BuildServiceProvider();
        }

        private static async Task<ApplicationUser> SetupAuthorizedAdminAsync(AsyncServiceScope scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var role = await roles.FindByNameAsync(DomainValues.Roles.Administrator);
            if (role == null)
            {
                role = new IdentityRole(DomainValues.Roles.Administrator);
                var roleResult = await roles.CreateAsync(role);
                Assert.True(roleResult.Succeeded);
            }

            var admin = await AddUserAsync(users, "ADMIN-" + Guid.NewGuid().ToString("N")[..8]);
            Assert.True((await users.AddToRoleAsync(admin, role.Name)).Succeeded);

            var permission = await db.Permissions.SingleOrDefaultAsync(row =>
                row.PermissionName == DomainValues.Permissions.EquipmentManage);
            if (permission == null)
            {
                permission = new Permission
                {
                    PermissionName = DomainValues.Permissions.EquipmentManage,
                    Description = "Manage equipment"
                };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();
            }

            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();
            return admin;
        }

        private static async Task<ApplicationUser> AddUserAsync(
            UserManager<ApplicationUser> users,
            string code)
        {
            var user = new ApplicationUser
            {
                UserName = code,
                UserCode = code,
                Email = $"{code.ToLowerInvariant()}@test.local",
                FirstName = code,
                LastName = "Test",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, "S3cret!TestPassword");
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return user;
        }

        private static async Task<EquipmentCategory> CreateCategoryAsync(
            AsiBasecodeDBContext db,
            bool isActive = true)
        {
            var category = new EquipmentCategory
            {
                CategoryCode = "CAT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                CategoryName = "Test equipment " + Guid.NewGuid().ToString("N")[..8],
                IsActive = isActive
            };
            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();
            return category;
        }

        private static async Task<EquipmentItem> CreateEquipmentAsync(
            AsiBasecodeDBContext db,
            long categoryId,
            string imageUrl = null,
            string serialNumber = null)
        {
            var item = new EquipmentItem
            {
                CategoryId = categoryId,
                ItemCode = "EQ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                ItemName = "Test equipment",
                Location = "Test store",
                ConditionStatus = DomainValues.ReturnConditions.Good,
                ItemStatus = DomainValues.EquipmentStatuses.Available,
                ImageUrl = imageUrl,
                SerialNumber = serialNumber,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.EquipmentItems.Add(item);
            await db.SaveChangesAsync();
            return item;
        }

        private static async Task AddReservationAsync(
            AsyncServiceScope scope,
            long equipmentId,
            string status,
            DateTime reservationEnd)
        {
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var borrower = await AddUserAsync(users, "BORROWER-" + Guid.NewGuid().ToString("N")[..8]);
            var profile = new BorrowerProfile
            {
                UserId = borrower.Id,
                Department = "Testing",
                IsEligible = true
            };
            db.BorrowerProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.Reservations.Add(new Reservation
            {
                BorrowerProfileId = profile.BorrowerProfileId,
                EquipmentId = equipmentId,
                ReservationStart = reservationEnd.AddDays(-2),
                ReservationEnd = reservationEnd,
                Purpose = "Automated test reservation",
                Status = status
            });
            await db.SaveChangesAsync();
        }

        private static EquipmentItemFormViewModel NewItemModel(long categoryId) => new()
        {
            CategoryId = categoryId,
            ItemCode = "EQ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            ItemName = "Portable projector",
            Location = "Main store",
            ConditionStatus = DomainValues.ReturnConditions.Good,
            ItemStatus = DomainValues.EquipmentStatuses.Available
        };
    }
}
