using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    // SQL tests only create/drop a uniquely named disposable database, never the application's database.
    public sealed class AdministrationTestEnvironment : IAsyncDisposable
    {
        private readonly SqliteDb sqlite;
        private readonly string sqlConnection;
        public ServiceProvider Provider { get; private set; }
        public string ActorId { get; private set; }
        public AuditFailureInterceptor Fault { get; } = new();

        private AdministrationTestEnvironment(bool sqlServer)
        {
            if (sqlServer)
                sqlConnection = @"Server=(localdb)\MSSQLLocalDB;Database=Gearantee_Pr22_Test_" +
                    Guid.NewGuid().ToString("N") + ";Trusted_Connection=True;TrustServerCertificate=True";
            else sqlite = new SqliteDb();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (sqlite != null)
            {
                services.AddDbContext<SqliteDashboardDbContext>(options => options.UseSqlite(sqlite.Connection).AddInterceptors(Fault));
                services.AddScoped<AsiBasecodeDBContext>(provider => provider.GetRequiredService<SqliteDashboardDbContext>());
                services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<SqliteDashboardDbContext>().AddDefaultTokenProviders();
            }
            else
            {
                services.AddDbContext<AsiBasecodeDBContext>(options => options.UseSqlServer(sqlConnection).AddInterceptors(Fault));
                services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<AsiBasecodeDBContext>().AddDefaultTokenProviders();
            }
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        }

        public static async Task<AdministrationTestEnvironment> CreateAsync(bool sqlServer = false)
        {
            var environment = new AdministrationTestEnvironment(sqlServer);
            var services = new ServiceCollection();
            environment.ConfigureServices(services);
            environment.Provider = services.BuildServiceProvider();
            try
            {
                using var scope = environment.Provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                if (sqlServer) await db.Database.MigrateAsync();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var name in new[] { "Administrator", "Borrower", "Custodian" })
                    Assert.True((await roles.CreateAsync(new IdentityRole(name))).Succeeded);
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var actor = new ApplicationUser
                {
                    UserName = "ADMIN-TEST", UserCode = "ADMIN-TEST", Email = "admin@test.local",
                    FirstName = "Admin", LastName = "Test", IsActive = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                };
                Assert.True((await users.CreateAsync(actor, "Admin!123456")).Succeeded);
                Assert.True((await users.AddToRoleAsync(actor, "Administrator")).Succeeded);
                environment.ActorId = actor.Id;
                var permission = new Permission { PermissionName = DomainValues.Permissions.UserRoleManage, Description = "Manage users" };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = (await roles.FindByNameAsync("Administrator")).Id, PermissionId = permission.PermissionId
                });
                await db.SaveChangesAsync();
                return environment;
            }
            catch { await environment.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            if (sqlConnection != null && Provider != null)
            {
                await using var scope = Provider.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>().Database.EnsureDeletedAsync();
            }
            if (Provider != null) await Provider.DisposeAsync();
            sqlite?.Dispose();
        }
    }

    public sealed class AuditFailureInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public int AuditAttempts { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context.ChangeTracker.Entries<AdministrationAuditEvent>()
                .Any(entry => entry.State == EntityState.Added) && ++AuditAttempts == 2)
                throw new DbUpdateException("Simulated second audit failure.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GEARANTEE_TEST_SQLSERVER") != "1")
                Skip = "Set GEARANTEE_TEST_SQLSERVER=1 on Windows with LocalDB to run isolated SQL Server tests.";
        }
    }
}
