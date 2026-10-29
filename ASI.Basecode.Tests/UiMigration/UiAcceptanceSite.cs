using ASI.Basecode.Data;
// OTP store moved to Data
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Authorization;
using ASI.Basecode.WebApp.Controllers;
using ASI.Basecode.Data.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UiMigration
{
    // This host uses real MVC/Identity/services and a disposable SQLite database.
    // It never connects to the configured application database or sends email.
    internal sealed class UiAcceptanceSite : IAsyncDisposable
    {
        public const string Password = "UiFixture!2026";
        private readonly SqliteDb sqlite = new();
        public WebApplication App { get; private set; }
        public string Root { get; private set; }
        public long PendingId { get; private set; }
        public long PickupId { get; private set; }
        public long RejectedId { get; private set; }
        public long ReleaseId { get; private set; }
        public long ReturnedId { get; private set; }
        public long CategoryId { get; private set; }
        public long AvailableItemId { get; private set; }
        public long LockedItemId { get; private set; }
        public long BorrowerProfileId { get; private set; }
        public string BorrowerId { get; private set; }
        public Exception LastRequestFailure { get; private set; }

        public static async Task<UiAcceptanceSite> CreateAsync()
        {
            var site = new UiAcceptanceSite();
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "ASI.Basecode.sln"))) root = root.Parent;
            if (root == null) throw new InvalidOperationException("Repository root not found.");
            site.Root = root.FullName;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(DashboardController).Assembly.GetName().Name,
                ContentRootPath = Path.Combine(site.Root, "ASI.Basecode.WebApp"),
                EnvironmentName = "Testing"
            });
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
            builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("GEARANTEE_UI_PREVIEW") == "1"
                ? "http://127.0.0.1:5192" : "http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["SeedAdmin:Email"] = "admin@ui-fixture.invalid", ["SeedAdmin:UserCode"] = "UI-ADMIN", ["SeedAdmin:Password"] = Password,
                ["SeedCustodian:Email"] = "custodian@ui-fixture.invalid", ["SeedCustodian:UserCode"] = "UI-CUST", ["SeedCustodian:Password"] = Password,
                ["SeedDemo:Email"] = "demo@ui-fixture.invalid", ["SeedDemo:Password"] = Password
            });
            var services = builder.Services;
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.AddHttpContextAccessor();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<SqliteDashboardDbContext>(options => options.UseSqlite(site.sqlite.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider => provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            }).AddEntityFrameworkStores<SqliteDashboardDbContext>().AddDefaultTokenProviders();
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "Gearantee.UiAcceptance." + Guid.NewGuid().ToString("N");
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
            });
            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationClaimsPrincipalFactory>();
            services.AddAuthorization(options =>
            {
                foreach (var permission in new[]
                {
                    DomainValues.Permissions.EquipmentBrowse, DomainValues.Permissions.ReservationCreate,
                    DomainValues.Permissions.ReservationReview, DomainValues.Permissions.TransactionReleaseReturn,
                    DomainValues.Permissions.EquipmentManage, DomainValues.Permissions.BorrowerManage,
                    DomainValues.Permissions.UserRoleManage, DomainValues.Permissions.HistoryView
                })
                    options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            });
            services.AddControllersWithViews(options => options.Filters.Add(new AuthorizeFilter(
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())))
                .AddApplicationPart(typeof(DashboardController).Assembly).AddRazorRuntimeCompilation();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
            services.AddScoped<IBorrowerProfileRepository, BorrowerProfileRepository>();
            services.AddScoped<IReservationRepository, ReservationRepository>();
            services.AddScoped<IReturnRecordRepository, ReturnRecordRepository>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();
            services.AddScoped<IBorrowerProfileService, BorrowerProfileService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IEquipmentItemService, EquipmentItemService>();
            services.AddScoped<IAvailabilityCalendarService, AvailabilityCalendarService>();
            services.AddScoped<ICatalogService, CatalogService>();
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<IReturnRecordService, ReturnRecordService>();
            services.AddScoped<IBorrowingHistoryService, BorrowingHistoryService>();
            services.AddScoped<IPasswordResetOtpStore, IdentityPasswordResetOtpStore>();
            services.AddScoped<IPasswordResetOtpService, PasswordResetOtpService>();
            services.AddSingleton<IBrevoEmailSender, NoExternalEmail>();
            site.App = builder.Build();
            site.App.UseDeveloperExceptionPage();
            site.App.Use(async (context, next) =>
            {
                try { await next(context); }
                catch (Exception error) { site.LastRequestFailure = error; throw; }
            });
            site.App.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(site.Root, "ASI.Basecode.WebApp/wwwroot")) });
            site.App.UseRouting();
            site.App.UseSession();
            site.App.UseAuthentication();
            site.App.UseAuthorization();
            site.App.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
            try
            {
                await DatabaseSeeder.SeedAsync(site.App.Services, builder.Configuration);
                await DatabaseSeeder.SeedDemoAsync(site.App.Services, builder.Configuration);
                using (var scope = site.App.Services.CreateScope())
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                    var borrower = new ApplicationUser
                    {
                        UserName = "UI-BORROWER", UserCode = "UI-BORROWER", Email = "borrower@ui-fixture.invalid",
                        FirstName = "Mara", LastName = "Villanueva", IsActive = true, EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    };
                    Assert.True((await users.CreateAsync(borrower, Password)).Succeeded);
                    Assert.True((await users.AddToRoleAsync(borrower, DomainValues.Roles.Borrower)).Succeeded);
                    var profile = DashboardSeed.Profile(db, borrower);
                    site.BorrowerId = borrower.Id;
                    site.BorrowerProfileId = profile.BorrowerProfileId;
                    site.CategoryId = db.EquipmentCategories.Select(item => item.CategoryId).First();
                    site.AvailableItemId = db.EquipmentItems.Where(item => item.ItemCode == "DEMO-PROJ-002").Select(item => item.EquipmentId).Single();
                    site.LockedItemId = db.EquipmentItems.Where(item => item.ItemCode == "DEMO-LAP-001").Select(item => item.EquipmentId).Single();
                    site.PendingId = db.Reservations.Where(item => item.Status == "Pending").Select(item => item.ReservationId).First();
                    site.PickupId = db.Reservations.Where(item => item.Purpose == "DEMO: Approved pickup today").Select(item => item.ReservationId).Single();
                    site.RejectedId = db.Reservations.Where(item => item.Status == "Rejected").Select(item => item.ReservationId).Single();
                    site.ReleaseId = db.ReleaseRecords.Where(item => !db.ReturnRecords.Any(record => record.ReleaseRecordId == item.ReleaseRecordId)).Select(item => item.ReleaseRecordId).First();
                    site.ReturnedId = db.ReturnRecords.Select(item => item.ReturnRecordId).First();
                    var longItem = await db.EquipmentItems.SingleAsync(item => item.EquipmentId == site.AvailableItemId);
                    longItem.ItemName = "Portable projector with HDMI adapter, remote control, carrying case and complete presentation accessories";
                    longItem.Description = "Presentation equipment with a long inventory description.\nInspect all included accessories before release and return.";
                    await db.SaveChangesAsync();
                }
                await site.App.StartAsync();
                return site;
            }
            catch { await site.DisposeAsync(); throw; }
        }

        public HttpClient NewClient() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        { BaseAddress = new Uri(App.Urls.Single()), Timeout = TimeSpan.FromSeconds(45) };

        public async Task LoginAsync(HttpClient client, string user)
        {
            var html = await client.GetStringAsync("/Account/Login");
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserId"] = user, ["Password"] = Password, ["__RequestVerificationToken"] = Token(html)
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("Dashboard", response.Headers.Location.ToString());
        }

        public static string Token(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        public async ValueTask DisposeAsync()
        {
            if (App != null) await App.DisposeAsync();
            sqlite.Dispose();
        }

        private sealed class NoExternalEmail : IBrevoEmailSender
        {
            public Task SendPasswordResetOtpAsync(string email, string name, string otp, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("External email is disabled in the UI acceptance fixture.");
        }
    }
}
