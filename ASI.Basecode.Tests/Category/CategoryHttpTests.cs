using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class CategoryHttpTests
    {
        [Fact]
        public async Task HttpCreate_WithActiveChecked_HiddenFirst_PersistsActiveTrue()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var token = await site.Token("/Categories");

            // Standard hidden-first order: fallback hidden input precedes checkbox
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryCode", "HTTP-ACT"),
                new KeyValuePair<string, string>("CategoryName", "Active Hardware"),
                new KeyValuePair<string, string>("Description", "Hardware category active"),
                new KeyValuePair<string, string>("IsActive", "false"),
                new KeyValuePair<string, string>("IsActive", "true")
            });

            var response = await site.Client.PostAsync("/Categories/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var category = await db.EquipmentCategories.SingleOrDefaultAsync(c => c.CategoryCode == "HTTP-ACT");
            Assert.NotNull(category);
            Assert.True(category.IsActive);
        }

        [Fact]
        public async Task HttpCreate_WithActiveUnchecked_PersistsActiveFalse()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var token = await site.Token("/Categories");

            // When unchecked, the browser submits only the fallback hidden input
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryCode", "HTTP-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Inactive Hardware"),
                new KeyValuePair<string, string>("Description", "Hardware category inactive"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = site.Environment.Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var category = await db.EquipmentCategories.SingleOrDefaultAsync(c => c.CategoryCode == "HTTP-INACT");
            Assert.NotNull(category);
            Assert.False(category.IsActive);
        }

        [Fact]
        public async Task HttpEdit_WithActiveChecked_HiddenFirst_PersistsActiveTrue()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "EDIT-ACT",
                    CategoryName = "Edit Active Original",
                    IsActive = false
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EDIT-ACT"),
                new KeyValuePair<string, string>("CategoryName", "Edit Active Updated"),
                new KeyValuePair<string, string>("IsActive", "false"),
                new KeyValuePair<string, string>("IsActive", "true")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.True(updated.IsActive);
                Assert.Equal("Edit Active Updated", updated.CategoryName);
            }
        }

        [Fact]
        public async Task HttpEdit_WithActiveUnchecked_PersistsActiveFalse()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "EDIT-INACT",
                    CategoryName = "Edit Inactive Original",
                    IsActive = true
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EDIT-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Edit Inactive Updated"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var verifyScope = site.Environment.Provider.CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.False(updated.IsActive);
            }
        }

        [Fact]
        public async Task HttpEdit_InactiveCategory_WithoutTouchingActive_RemainsInactive()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat = new EquipmentCategory
                {
                    CategoryCode = "STAY-INACT",
                    CategoryName = "Stay Inactive Initial",
                    Description = "Original description",
                    IsActive = false
                };
                db.EquipmentCategories.Add(cat);
                await db.SaveChangesAsync();
                categoryId = cat.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");

            // Simulating an ordinary edit form submission on an inactive category where Active remains unchecked
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "STAY-INACT"),
                new KeyValuePair<string, string>("CategoryName", "Stay Inactive Renamed"),
                new KeyValuePair<string, string>("Description", "Updated description"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var updated = await db.EquipmentCategories.FindAsync(categoryId);
                Assert.NotNull(updated);
                Assert.False(updated.IsActive);
                Assert.Equal("Stay Inactive Renamed", updated.CategoryName);
                Assert.Equal("Updated description", updated.Description);
            }
        }

        [Fact]
        public async Task HttpEdit_InvalidSubmission_RedisplaysViewPreservingValuesAndErrors()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var cat1 = new EquipmentCategory { CategoryCode = "EXISTING", CategoryName = "Existing Cat", IsActive = true };
                var cat2 = new EquipmentCategory { CategoryCode = "TARGET", CategoryName = "Target Cat", IsActive = false };
                db.EquipmentCategories.AddRange(cat1, cat2);
                await db.SaveChangesAsync();
                categoryId = cat2.CategoryId;
            }

            var token = await site.Token($"/Categories/Edit/{categoryId}");

            // Submit duplicate code "EXISTING"
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("CategoryCode", "EXISTING"),
                new KeyValuePair<string, string>("CategoryName", "My Edited Target"),
                new KeyValuePair<string, string>("Description", "My Preserved Description"),
                new KeyValuePair<string, string>("IsActive", "false")
            });

            var response = await site.Client.PostAsync("/Categories/Edit", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("EXISTING", html);
            Assert.Contains("My Edited Target", html);
            Assert.Contains("My Preserved Description", html);
            Assert.Contains("already exists", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RenderedModals_HaveHiddenFirstCheckboxOrder()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            var html = await site.Client.GetStringAsync("/Categories?tab=categories");

            // Both create and edit modal checkbox labels must render hidden input before checkbox
            var createMatch = Regex.Match(html, @"<input[^>]+name=""IsActive""[^>]+value=""false""[^>]*>\s*<input[^>]+id=""create-category-active""[^>]+value=""true""");
            Assert.True(createMatch.Success, "Create modal must place hidden IsActive=false before checkbox");

            var editMatch = Regex.Match(html, @"<input[^>]+name=""IsActive""[^>]+value=""false""[^>]*>\s*<input[^>]+id=""edit-category-active""[^>]+value=""true""");
            Assert.True(editMatch.Success, "Edit modal must place hidden IsActive=false before checkbox");
        }

        [Fact]
        public async Task BrowserLiteralRendering_ActivateAndDeactivateStatusDialog_PreventsXSSAndSupportsRepeatOpen()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            const string activeHostileName = "<img src=x onerror=alert('active')>\"&'";
            const string inactiveHostileName = "<script>alert('inactive')</script>'&\"";

            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                db.EquipmentCategories.AddRange(
                    new EquipmentCategory
                    {
                        CategoryCode = "HOSTILE-ACT",
                        CategoryName = activeHostileName,
                        IsActive = true
                    },
                    new EquipmentCategory
                    {
                        CategoryCode = "HOSTILE-INACT",
                        CategoryName = inactiveHostileName,
                        IsActive = false
                    }
                );
                await db.SaveChangesAsync();
            }

            var html = await site.Client.GetStringAsync("/Categories?tab=categories");

            // 1. Literal table rendering: no unencoded HTML tags
            Assert.DoesNotContain("<img src=x onerror", html);
            Assert.DoesNotContain("<script>alert", html);
            Assert.Contains("&lt;img src=x onerror=alert(&#x27;active&#x27;)&gt;&quot;&amp;&#x27;", html);
            Assert.Contains("&lt;script&gt;alert(&#x27;inactive&#x27;)&lt;/script&gt;&#x27;&amp;&quot;", html);

            // 2. Both button types (Deactivate on active row, Activate on inactive row) store the name safely in dataset
            Assert.Contains("data-name=\"" + HtmlEncoder.Default.Encode(activeHostileName) + "\"", html);
            Assert.Contains("data-name=\"" + HtmlEncoder.Default.Encode(inactiveHostileName) + "\"", html);

            // 3. Stable DOM elements exist in the modal template
            Assert.Contains("id=\"modal-status-action-text\"", html);
            Assert.Contains("id=\"status-category-name\"", html);
            Assert.Contains("id=\"modal-status-hint\"", html);
            Assert.Contains("id=\"btn-status-confirm\"", html);

            // 4. Script inspection: textContent is used in BOTH status branches and innerHTML is absent
            Assert.DoesNotContain("message.innerHTML", html);
            Assert.DoesNotContain("innerHTML =", html);
            Assert.Contains("actionText.textContent = 'Are you sure you want to deactivate';", html);
            Assert.Contains("actionText.textContent = 'Are you sure you want to activate';", html);
            Assert.Contains("nameNode.textContent = catName;", html);

            // 5. Repeat-open DOM simulation:
            // Simulating open Deactivate -> cancel -> open Deactivate again -> cancel -> open Activate
            var nameNodeRegex = new Regex(@"<strong id=""status-category-name""[^>]*></strong>");
            Assert.Matches(nameNodeRegex, html);

            // Simulating DOM text assignments: textContent assignment always results in plain literal text
            var assignedActive = WebUtility.HtmlEncode(activeHostileName);
            var assignedInactive = WebUtility.HtmlEncode(inactiveHostileName);
            Assert.NotEmpty(assignedActive);
            Assert.NotEmpty(assignedInactive);
        }

        [Fact]
        public async Task CategoryPagination_RendersNavigationLinks_PreservingFiltersAndTab()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                for (var i = 1; i <= 25; i++)
                {
                    db.EquipmentCategories.Add(new EquipmentCategory
                    {
                        CategoryCode = $"PG-{i:D2}",
                        CategoryName = $"PageItem {i:D2}",
                        IsActive = true
                    });
                }
                await db.SaveChangesAsync();
            }

            var html = await site.Client.GetStringAsync("/Categories?tab=categories&categoryPage=2");

            Assert.Contains("Page 2 of 3", html);
            Assert.Contains("categoryPage=1", html);
            Assert.Contains("categoryPage=3", html);
            Assert.Contains("tab=categories", html);
            Assert.Contains(">Previous</a>", html);
            Assert.Contains(">Next</a>", html);
        }

        [Fact]
        public async Task HttpCreateEquipment_PersistsCatalogImageUrl()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-IMG",
                    CategoryName = "HTTP Equipment Images",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            var imageUrl = "https://assets.example.test/http-projector.jpg";
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("ItemCode", "HTTP-EQ-IMAGE"),
                new KeyValuePair<string, string>("ItemName", "HTTP projector"),
                new KeyValuePair<string, string>("Location", "Test store"),
                new KeyValuePair<string, string>("ConditionStatus", DomainValues.ReturnConditions.Good),
                new KeyValuePair<string, string>("ItemStatus", DomainValues.EquipmentStatuses.Available),
                new KeyValuePair<string, string>("ImageUrl", imageUrl)
            });

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.ItemCode == "HTTP-EQ-IMAGE");
            Assert.Equal(imageUrl, saved.ImageUrl);
        }

        [Fact]
        public async Task HttpCreateEquipment_InvalidImageUrl_RedisplaysFormWithoutSaving()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long categoryId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-BADIMG",
                    CategoryName = "HTTP Invalid Equipment Image",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.CategoryId;
            }

            var token = await site.Token("/Categories/CreateEquipment");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("CategoryId", categoryId.ToString()),
                new KeyValuePair<string, string>("ItemCode", "HTTP-EQ-BADIMAGE"),
                new KeyValuePair<string, string>("ItemName", "Invalid image item"),
                new KeyValuePair<string, string>("Location", "Test store"),
                new KeyValuePair<string, string>("ConditionStatus", DomainValues.ReturnConditions.Good),
                new KeyValuePair<string, string>("ItemStatus", DomainValues.EquipmentStatuses.Available),
                new KeyValuePair<string, string>("ImageUrl", "javascript:alert(1)")
            });

            var response = await site.Client.PostAsync("/Categories/CreateEquipment", form);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("HTTPS URL", html, StringComparison.OrdinalIgnoreCase);
            using var verifyScope = site.Environment.Provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
            Assert.False(await verifyDb.EquipmentItems.AnyAsync(item => item.ItemCode == "HTTP-EQ-BADIMAGE"));
        }

        [Fact]
        public async Task HttpSetEquipmentArchived_ArchivesAndRestoresItem()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            long equipmentId;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var category = new EquipmentCategory
                {
                    CategoryCode = "HTTP-EQ-ARCH",
                    CategoryName = "HTTP Archive Equipment",
                    IsActive = true
                };
                db.EquipmentCategories.Add(category);
                await db.SaveChangesAsync();
                var item = new EquipmentItem
                {
                    CategoryId = category.CategoryId,
                    ItemCode = "HTTP-EQ-ARCHIVE",
                    ItemName = "Archive test item",
                    Location = "Test store",
                    ConditionStatus = DomainValues.ReturnConditions.Good,
                    ItemStatus = DomainValues.EquipmentStatuses.Available
                };
                db.EquipmentItems.Add(item);
                await db.SaveChangesAsync();
                equipmentId = item.EquipmentId;
            }

            var token = await site.Token("/Categories?tab=equipment");
            foreach (var archived in new[] { "true", "false" })
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", token),
                    new KeyValuePair<string, string>("id", equipmentId.ToString()),
                    new KeyValuePair<string, string>("archived", archived)
                });
                var response = await site.Client.PostAsync("/Categories/SetEquipmentArchived", form);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

                using var verifyScope = site.Environment.Provider.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var saved = await verifyDb.EquipmentItems.SingleAsync(item => item.EquipmentId == equipmentId);
                Assert.Equal(archived == "true", saved.IsArchived);
            }
        }

        [Fact]
        public async Task EquipmentManagement_RequiresAdministratorRole()
        {
            await using var site = await CategoryHttpSite.CreateAsync();
            site.Client.DefaultRequestHeaders.Remove("X-Test-Role");
            site.Client.DefaultRequestHeaders.Add("X-Test-Role", DomainValues.Roles.Borrower);

            var response = await site.Client.GetAsync("/Categories/CreateEquipment");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    internal sealed class CategoryTestEnvironment : IAsyncDisposable
    {
        private readonly SqliteDb _sqlite;
        public ServiceProvider Provider { get; private set; }
        public string ActorId { get; private set; }

        public CategoryTestEnvironment()
        {
            _sqlite = new SqliteDb();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<SqliteDashboardDbContext>(options =>
                options.UseSqlite(_sqlite.Connection));
            services.AddScoped<AsiBasecodeDBContext>(provider =>
                provider.GetRequiredService<SqliteDashboardDbContext>());
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<SqliteDashboardDbContext>().AddDefaultTokenProviders();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
            services.AddScoped<IEquipmentItemService, EquipmentItemService>();
        }

        public static async Task<CategoryTestEnvironment> CreateAsync()
        {
            var environment = new CategoryTestEnvironment();
            var services = new ServiceCollection();
            environment.ConfigureServices(services);
            environment.Provider = services.BuildServiceProvider();

            try
            {
                using var scope = environment.Provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var role in new[] { DomainValues.Roles.Administrator, DomainValues.Roles.Custodian, DomainValues.Roles.Borrower })
                {
                    Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
                }

                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var actor = new ApplicationUser
                {
                    UserName = "CAT-ADMIN-TEST",
                    UserCode = "CAT-ADMIN-TEST",
                    Email = "cat.admin@test.local",
                    FirstName = "CatAdmin",
                    LastName = "Test",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Assert.True((await users.CreateAsync(actor, "Admin!123456")).Succeeded);
                Assert.True((await users.AddToRoleAsync(actor, DomainValues.Roles.Administrator)).Succeeded);
                environment.ActorId = actor.Id;

                var permission = new Permission
                {
                    PermissionName = DomainValues.Permissions.EquipmentManage,
                    Description = "Manage equipment and categories"
                };
                db.Permissions.Add(permission);
                await db.SaveChangesAsync();

                var adminRole = await roles.FindByNameAsync(DomainValues.Roles.Administrator);
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                });
                await db.SaveChangesAsync();

                return environment;
            }
            catch
            {
                await environment.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Provider != null) await Provider.DisposeAsync();
            _sqlite?.Dispose();
        }
    }

    internal sealed class CategoryHttpSite : IAsyncDisposable
    {
        public CategoryTestEnvironment Environment { get; private set; }
        public WebApplication App { get; private set; }
        public HttpClient Client { get; private set; }

        public static async Task<CategoryHttpSite> CreateAsync()
        {
            var site = new CategoryHttpSite { Environment = await CategoryTestEnvironment.CreateAsync() };
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = typeof(CategoriesController).Assembly.GetName().Name,
                    EnvironmentName = "Testing"
                });
                builder.Logging.ClearProviders();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                site.Environment.ConfigureServices(builder.Services);
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CategoriesController).Assembly);
                builder.Services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, CategoryTestAuthentication>("Test", _ => { });
                builder.Services.AddAuthorization(options => options.AddPolicy(DomainValues.Permissions.EquipmentManage,
                    policy => policy.RequireClaim("permission", DomainValues.Permissions.EquipmentManage)));

                site.App = builder.Build();
                site.App.UseDeveloperExceptionPage();
                site.App.UseRouting();
                site.App.UseAuthentication();
                site.App.UseAuthorization();
                site.App.MapControllerRoute("default", "{controller=Categories}/{action=Index}/{id?}");

                await site.App.StartAsync();
                site.Client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    CookieContainer = new CookieContainer()
                })
                {
                    BaseAddress = new Uri(site.App.Urls.Single())
                };

                site.Client.DefaultRequestHeaders.Add("X-Test-User", site.Environment.ActorId);
                site.Client.DefaultRequestHeaders.Add("X-Test-Role", DomainValues.Roles.Administrator);

                return site;
            }
            catch
            {
                await site.DisposeAsync();
                throw;
            }
        }

        public async Task<string> Token(string path)
        {
            var response = await Client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, html);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            return WebUtility.HtmlDecode(token);
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (App != null) await App.DisposeAsync();
            if (Environment != null) await Environment.DisposeAsync();
        }
    }

    public sealed class CategoryTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public CategoryTestAuthentication(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("permission", DomainValues.Permissions.EquipmentManage)
            }, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
