using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
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
    public class CategoryServiceTests
    {
        [Fact]
        public async Task CanManageAsync_RequiresActiveAdminWithEquipmentManagePermission()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var admin = await AddUserAsync(users, "ADM-CAT-01", "admin.cat@test.local");
            Assert.True((await users.AddToRoleAsync(admin, adminRole.Name)).Succeeded);

            var permission = AddPermission(db, DomainValues.Permissions.EquipmentManage);
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            Assert.True(await service.CanManageAsync(admin.Id));

            // Remove permission -> Should return false
            db.RolePermissions.RemoveRange(db.RolePermissions);
            await db.SaveChangesAsync();
            Assert.False(await service.CanManageAsync(admin.Id));

            // Restore permission but deactivate user -> Should return false
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permission.PermissionId
            });
            await db.SaveChangesAsync();

            admin.IsActive = false;
            await users.UpdateAsync(admin);
            Assert.False(await service.CanManageAsync(admin.Id));
        }

        [Fact]
        public async Task CreateCategoryAsync_ValidInput_CreatesCategorySuccessfully()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            var model = new CreateCategoryViewModel
            {
                CategoryCode = "LAP",
                CategoryName = "Laptops & Notebooks",
                Description = "High performance laptops for loan.",
                IsActive = true
            };

            var result = await service.CreateCategoryAsync(admin.Id, model);

            Assert.True(result.Succeeded, result.Message);
            Assert.True(result.CategoryId > 0);

            var created = await db.EquipmentCategories.FindAsync(result.CategoryId);
            Assert.NotNull(created);
            Assert.Equal("LAP", created.CategoryCode);
            Assert.Equal("Laptops & Notebooks", created.CategoryName);
            Assert.Equal("High performance laptops for loan.", created.Description);
            Assert.True(created.IsActive);
        }

        [Fact]
        public async Task CreateCategoryAsync_DuplicateCode_ReturnsFailure()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            db.EquipmentCategories.Add(new EquipmentCategory
            {
                CategoryCode = "CAM",
                CategoryName = "Digital Cameras",
                IsActive = true
            });
            await db.SaveChangesAsync();

            var model = new CreateCategoryViewModel
            {
                CategoryCode = "cam", // Lowercase duplicate check
                CategoryName = "Video Cameras",
                IsActive = true
            };

            var result = await service.CreateCategoryAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateCategoryAsync_DuplicateName_ReturnsFailure()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            db.EquipmentCategories.Add(new EquipmentCategory
            {
                CategoryCode = "PROJ-01",
                CategoryName = "Projectors",
                IsActive = true
            });
            await db.SaveChangesAsync();

            var model = new CreateCategoryViewModel
            {
                CategoryCode = "PROJ-02",
                CategoryName = "projectors", // Case-insensitive duplicate check
                IsActive = true
            };

            var result = await service.CreateCategoryAsync(admin.Id, model);

            Assert.False(result.Succeeded);
            Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateCategoryAsync_UnauthorizedUser_ReturnsAccessDenied()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var user = await AddUserAsync(users, "BORROWER-01", "borrower@test.local");

            var model = new CreateCategoryViewModel
            {
                CategoryCode = "AUD",
                CategoryName = "Audio Equipment",
                IsActive = true
            };

            var result = await service.CreateCategoryAsync(user.Id, model);

            Assert.False(result.Succeeded);
            Assert.True(result.Forbidden);
        }

        [Fact]
        public async Task GetCategoriesAsync_FiltersSearchAndStatus()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            db.EquipmentCategories.AddRange(
                new EquipmentCategory { CategoryCode = "LAP", CategoryName = "Laptops", IsActive = true },
                new EquipmentCategory { CategoryCode = "CAM", CategoryName = "Cameras", IsActive = true },
                new EquipmentCategory { CategoryCode = "MIC", CategoryName = "Microphones", IsActive = false }
            );
            await db.SaveChangesAsync();

            // Search by name
            var searchResult = await service.GetCategoriesAsync(admin.Id, search: "lap");
            Assert.Single(searchResult.Categories);
            Assert.Equal("LAP", searchResult.Categories[0].CategoryCode);

            // Filter active
            var activeResult = await service.GetCategoriesAsync(admin.Id, status: "active");
            Assert.Equal(2, activeResult.Categories.Count);

            // Filter inactive
            var inactiveResult = await service.GetCategoriesAsync(admin.Id, status: "inactive");
            Assert.Single(inactiveResult.Categories);
            Assert.Equal("MIC", inactiveResult.Categories[0].CategoryCode);
        }

        [Fact]
        public async Task UpdateCategoryAsync_ValidInput_UpdatesCategorySuccessfully()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            var category = new EquipmentCategory
            {
                CategoryCode = "LAP-OLD",
                CategoryName = "Old Laptops",
                Description = "Original description",
                IsActive = true
            };
            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();

            var updateModel = new EditCategoryViewModel
            {
                CategoryId = category.CategoryId,
                CategoryCode = "LAP-NEW",
                CategoryName = "Refreshed Laptops",
                Description = "Updated description",
                IsActive = false
            };

            var result = await service.UpdateCategoryAsync(admin.Id, updateModel);

            Assert.True(result.Succeeded, result.Message);
            var updated = await db.EquipmentCategories.FindAsync(category.CategoryId);
            Assert.Equal("LAP-NEW", updated.CategoryCode);
            Assert.Equal("Refreshed Laptops", updated.CategoryName);
            Assert.Equal("Updated description", updated.Description);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task UpdateCategoryAsync_DuplicateCodeAcrossCategories_ReturnsFailure()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            var cat1 = new EquipmentCategory { CategoryCode = "CODE1", CategoryName = "Category One", IsActive = true };
            var cat2 = new EquipmentCategory { CategoryCode = "CODE2", CategoryName = "Category Two", IsActive = true };
            db.EquipmentCategories.AddRange(cat1, cat2);
            await db.SaveChangesAsync();

            var updateModel = new EditCategoryViewModel
            {
                CategoryId = cat2.CategoryId,
                CategoryCode = "code1", // Same as cat1
                CategoryName = "Different Name",
                IsActive = true
            };

            var result = await service.UpdateCategoryAsync(admin.Id, updateModel);

            Assert.False(result.Succeeded);
            Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetCategoryActiveAsync_TogglesCategoryStatus()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            var category = new EquipmentCategory
            {
                CategoryCode = "AUD-TOGGLE",
                CategoryName = "Audio Toggle",
                IsActive = true
            };
            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();

            // Deactivate
            var deactResult = await service.SetCategoryActiveAsync(admin.Id, category.CategoryId, false);
            Assert.True(deactResult.Succeeded);
            var reloaded1 = await db.EquipmentCategories.FindAsync(category.CategoryId);
            Assert.False(reloaded1.IsActive);

            // Reactivate
            var reactResult = await service.SetCategoryActiveAsync(admin.Id, category.CategoryId, true);
            Assert.True(reactResult.Succeeded);
            var reloaded2 = await db.EquipmentCategories.FindAsync(category.CategoryId);
            Assert.True(reloaded2.IsActive);
        }

        [Fact]
        public async Task GetEquipmentManagementAsync_PaginatesCategories_With11And21Records()
        {
            using var database = new SqliteDb();
            using var provider = CreateProvider(database);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            var admin = await SetupAuthorizedAdminAsync(scope);

            for (var i = 1; i <= 11; i++)
            {
                db.EquipmentCategories.Add(new EquipmentCategory
                {
                    CategoryCode = $"CAT-{i:D2}",
                    CategoryName = $"Category {i:D2}",
                    IsActive = true
                });
            }
            await db.SaveChangesAsync();

            var page1 = await service.GetEquipmentManagementAsync(admin.Id, tab: "categories", categoryPage: 1);
            Assert.Equal(11, page1.CategoryTotalCount);
            Assert.Equal(1, page1.CategoryPage);
            Assert.Equal(2, page1.CategoryTotalPages);
            Assert.Equal(10, page1.Categories.Count);

            var page2 = await service.GetEquipmentManagementAsync(admin.Id, tab: "categories", categoryPage: 2);
            Assert.Equal(11, page2.CategoryTotalCount);
            Assert.Equal(2, page2.CategoryPage);
            Assert.Equal(2, page2.CategoryTotalPages);
            Assert.Single(page2.Categories);
            Assert.Equal("Category 11", page2.Categories[0].CategoryName);

            for (var i = 12; i <= 21; i++)
            {
                db.EquipmentCategories.Add(new EquipmentCategory
                {
                    CategoryCode = $"CAT-{i:D2}",
                    CategoryName = $"Category {i:D2}",
                    IsActive = true
                });
            }
            await db.SaveChangesAsync();

            var page3 = await service.GetEquipmentManagementAsync(admin.Id, tab: "categories", categoryPage: 3);
            Assert.Equal(21, page3.CategoryTotalCount);
            Assert.Equal(3, page3.CategoryPage);
            Assert.Equal(3, page3.CategoryTotalPages);
            Assert.Single(page3.Categories);
            Assert.Equal("Category 21", page3.Categories[0].CategoryName);
        }

        private static async Task<ApplicationUser> SetupAuthorizedAdminAsync(AsyncServiceScope scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var adminRole = await AddRoleAsync(roles, DomainValues.Roles.Administrator);
            var admin = await AddUserAsync(users, "ADMIN-" + Guid.NewGuid().ToString("N")[..6], "admin" + Guid.NewGuid().ToString("N")[..6] + "@test.local");
            Assert.True((await users.AddToRoleAsync(admin, adminRole.Name)).Succeeded);

            var permission = AddPermission(db, DomainValues.Permissions.EquipmentManage);
            if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.PermissionId))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                });
                await db.SaveChangesAsync();
            }

            return admin;
        }

        private static ServiceProvider CreateProvider(SqliteDb database)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddDataProtection();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(database.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<ICategoryService, CategoryService>();
            return services.BuildServiceProvider();
        }

        private static async Task<IdentityRole> AddRoleAsync(
            RoleManager<IdentityRole> roleManager,
            string name)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role != null) return role;

            role = new IdentityRole(name);
            var result = await roleManager.CreateAsync(role);
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return role;
        }

        private static async Task<ApplicationUser> AddUserAsync(
            UserManager<ApplicationUser> userManager,
            string code,
            string email)
        {
            var user = new ApplicationUser
            {
                UserName = code,
                UserCode = code,
                Email = email,
                FirstName = code,
                LastName = "Test",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, "S3cret!TestPassword");
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
            return user;
        }

        private static Permission AddPermission(AsiBasecodeDBContext db, string name)
        {
            var permission = db.Permissions.Local.FirstOrDefault(item => item.PermissionName == name) ??
                db.Permissions.FirstOrDefault(item => item.PermissionName == name);
            if (permission != null)
            {
                return permission;
            }

            permission = new Permission
            {
                PermissionName = name,
                Description = name
            };
            db.Permissions.Add(permission);
            db.SaveChanges();
            return permission;
        }
    }
}
