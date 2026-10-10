using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.WebApp.Controllers;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
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
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    public class UserAdministrationHttpTests
    {
        [Theory]
        [InlineData(null, HttpStatusCode.Unauthorized)]
        [InlineData("Borrower", HttpStatusCode.Forbidden)]
        [InlineData("Custodian", HttpStatusCode.Forbidden)]
        public async Task ImportRequiresAdminAtHttpBoundary(string role, HttpStatusCode expected)
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            site.Client.DefaultRequestHeaders.Remove("X-Test-Role");
            if (role != null) site.Client.DefaultRequestHeaders.Add("X-Test-Role", role);
            Assert.Equal(expected, (await site.Client.GetAsync("/Users/Import")).StatusCode);
            Assert.Equal(expected, (await site.Client.GetAsync("/Users/ImportTemplate")).StatusCode);
            Assert.Equal(expected, (await site.Client.PostAsync("/Users/Import", new MultipartFormDataContent())).StatusCode);
        }

        [Fact]
        public async Task ImportRequiresAntiforgeryAndDoesNotEchoMalformedSecret()
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            Assert.Equal(HttpStatusCode.BadRequest,
                (await site.Client.PostAsync("/Users/Import", new MultipartFormDataContent())).StatusCode);
            var token = await site.Token("/Users/Import");
            const string secret = "NeverEcho!123456";
            using var form = Upload(UserImportCsv.Header + "\n001,A,B,a@test.local,Custodian,\"" + secret, token);
            var response = await site.Client.PostAsync("/Users/Import", form);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No accounts were imported", html);
            Assert.DoesNotContain(secret, html);
        }

        [Theory]
        [InlineData("inactive")]
        [InlineData("permission")]
        public async Task StaleAdminClaimsCannotAuthorizeImport(string restriction)
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            var token = await site.Token("/Users/Import");
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                if (restriction == "inactive") (await db.Users.SingleAsync()).IsActive = false;
                else db.RolePermissions.RemoveRange(db.RolePermissions);
                await db.SaveChangesAsync();
            }
            using var form = Upload(UserImportCsv.Header + "\n0001,A,B,one@test.local,Custodian,Private!123456,,,", token);
            Assert.Equal(HttpStatusCode.Forbidden, (await site.Client.PostAsync("/Users/Import", form)).StatusCode);
        }

        [Fact]
        public async Task ValidMultipartImportCreatesAccountAndRedirectsWithoutPassword()
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            SavePreview("import.html", await site.Client.GetStringAsync("/Users/Import"));
            Assert.Equal(UserImportCsv.Header + "\r\n", await site.Client.GetStringAsync("/Users/ImportTemplate"));
            using var form = Upload(UserImportCsv.Header + "\n0001,A,B,one@test.local,Custodian,Private!123456,,,",
                await site.Token("/Users/Import"));
            var response = await site.Client.PostAsync("/Users/Import", form);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
            using var scope = site.Environment.Provider.CreateScope();
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>().Users.CountAsync());
            Assert.DoesNotContain("Private!123456", await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task InvalidEditRestoresServerOwnedStateAndPreservesSubmittedValuesAndStamp(bool active)
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            string id, stamp;
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
                Assert.True((await service.CreateAsync(site.Environment.ActorId, UserImportServiceTests.Account("TARGET"))).Succeeded);
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var user = await db.Users.SingleAsync(user => user.UserCode == "TARGET");
                id = user.Id; stamp = user.ConcurrencyStamp;
                user.IsActive = active;
                user.ConcurrencyStamp = Guid.NewGuid().ToString();
                await db.SaveChangesAsync();
            }
            var values = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await site.Token("/Users/Edit/" + id),
                ["Id"] = id, ["ConcurrencyStamp"] = stamp, ["FirstName"] = "Entered name", ["LastName"] = "",
                ["Email"] = "bad-email", ["SelectedRoles"] = "Custodian", ["UserCode"] = "FORGED", ["IsActive"] = (!active).ToString()
            };
            var response = await site.Client.PostAsync("/Users/Edit", new FormUrlEncodedContent(values));
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("TARGET", html);
            Assert.DoesNotContain("FORGED", html);
            Assert.Contains("Entered name", html);
            Assert.Contains("bad-email", html);
            Assert.Contains(active ? ">Deactivate</button>" : ">Reactivate</button>", html);
            Assert.Contains("value=\"" + stamp + "\"", html);
            Assert.Contains("field-validation-error", html);
            SavePreview(active ? "edit-active-errors.html" : "edit-inactive-errors.html", html);
        }

        [Fact]
        public async Task InvalidEditMissingTargetReturnsNotFound()
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            var response = await site.Client.PostAsync("/Users/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = "missing", ["__RequestVerificationToken"] = await site.Token("/Users/Import")
            }));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RazorPaginationAndSearchEncodeHostileTextAndRoundTripTheOriginalValue()
        {
            await using var site = await AdministrationHttpSite.CreateAsync();
            const string search = "\"><img src=x onerror=alert(1)>&+'";
            using (var scope = site.Environment.Provider.CreateScope())
            {
                var rows = Enumerable.Range(1, 21).Select(i =>
                {
                    var account = UserImportServiceTests.Account("SEARCH" + i);
                    account.FirstName = search;
                    return new UserImportRow(i + 1, account);
                }).ToArray();
                Assert.Equal(21, (await scope.ServiceProvider.GetRequiredService<IUserAdministrationService>()
                    .ImportAsync(site.Environment.ActorId, rows)).CreatedCount);
            }
            var html = await site.Client.GetStringAsync("/Users/Index?search=" + Uri.EscapeDataString(search) + "&page=2");
            Assert.DoesNotContain(search, html);
            Assert.DoesNotContain("<img src=x", html);
            var links = Regex.Matches(html, "href=\"([^\"]+)\"")
                .Select(match => new Uri(site.Client.BaseAddress, WebUtility.HtmlDecode(match.Groups[1].Value)))
                .Select(uri => QueryHelpers.ParseQuery(uri.Query))
                .Where(query => query.ContainsKey("page") && query.ContainsKey("search")).ToList();
            Assert.Equal(2, links.Count);
            Assert.All(links, query => Assert.Equal(search, query["search"].ToString()));
            Assert.Equal(new[] { "1", "3" }, links.Select(query => query["page"].ToString()).OrderBy(value => value));
        }

        private static MultipartFormDataContent Upload(string csv, string token)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(token), "__RequestVerificationToken");
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "csvFile", "accounts.csv");
            return form;
        }

        // Optional rendered artifacts for browser QA. Contains only disposable test accounts.
        private static void SavePreview(string fileName, string html)
        {
            var directory = Environment.GetEnvironmentVariable("GEARANTEE_UI_ARTIFACT_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, fileName), html);
        }
    }

    internal sealed class AdministrationHttpSite : IAsyncDisposable
    {
        public AdministrationTestEnvironment Environment { get; private set; }
        public WebApplication App { get; private set; }
        public HttpClient Client { get; private set; }
        public static async Task<AdministrationHttpSite> CreateAsync()
        {
            var site = new AdministrationHttpSite { Environment = await AdministrationTestEnvironment.CreateAsync() };
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = typeof(UsersController).Assembly.GetName().Name, EnvironmentName = "Testing"
                });
                builder.Logging.ClearProviders();
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                site.Environment.ConfigureServices(builder.Services);
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddControllersWithViews().AddApplicationPart(typeof(UsersController).Assembly);
                builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, AdministrationTestAuthentication>("Test", _ => { });
                builder.Services.AddAuthorization(options => options.AddPolicy(DomainValues.Permissions.UserRoleManage,
                    policy => policy.RequireClaim("permission", DomainValues.Permissions.UserRoleManage)));
                site.App = builder.Build();
                site.App.UseDeveloperExceptionPage();
                site.App.UseRouting();
                site.App.UseAuthentication();
                site.App.UseAuthorization();
                site.App.MapControllerRoute("default", "{controller=Users}/{action=Index}/{id?}");
                await site.App.StartAsync();
                site.Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
                    { BaseAddress = new Uri(site.App.Urls.Single()) };
                site.Client.DefaultRequestHeaders.Add("X-Test-User", site.Environment.ActorId);
                site.Client.DefaultRequestHeaders.Add("X-Test-Role", "Administrator");
                return site;
            }
            catch { await site.DisposeAsync(); throw; }
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

    // This handler is registered only in the test host; the application keeps its Identity authentication.
    public sealed class AdministrationTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdministrationTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("permission", DomainValues.Permissions.UserRoleManage)
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
