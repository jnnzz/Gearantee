using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Tests.Dashboard;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
using static ASI.Basecode.Tests.Reservations.ReservationTestHelpers;
using static ASI.Basecode.Tests.Returns.ReturnTestHelpers;

namespace ASI.Basecode.Tests.Returns
{
    /// <summary>Renders the real views through MVC to catch binding and markup errors.</summary>
    public sealed class ReturnRecordHttpTests
    {
        [Fact]
        public async Task Index_lists_open_loans_with_record_links()
        {
            await using var site = await ReturnHttpSite.CreateAsync();

            var html = await site.Html("/ReturnRecord");

            Assert.Contains("ON-TIME", html);
            Assert.Contains("OVERDUE", html);
            Assert.Contains($"/ReturnRecord/Create?releaseRecordId={site.OnTimeRelease}", html);

            var overdue = await site.Html("/ReturnRecord?tab=Overdue");
            Assert.Contains("OVERDUE", overdue);
            Assert.DoesNotContain("ON-TIME", overdue);
        }

        [Fact]
        public async Task Late_return_posts_redirects_and_shows_details()
        {
            await using var site = await ReturnHttpSite.CreateAsync();
            var token = await site.Token($"/ReturnRecord/Create?releaseRecordId={site.OverdueRelease}");

            var response = await site.Client.PostAsync("/ReturnRecord/Create", site.Form(token, site.OverdueRelease,
                DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.UnderMaintenance));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var details = await site.Html(response.Headers.Location.OriginalString);
            Assert.Contains("Late return", details);
            Assert.Contains("Under Maintenance", details);
            Assert.Contains("CUS-HTTP Test", details);
            Assert.Contains("Recorded at", details);
            Assert.DoesNotContain("back-dated", details);
        }

        [Fact]
        public async Task Refused_return_redisplays_the_form_with_the_reason()
        {
            await using var site = await ReturnHttpSite.CreateAsync();
            var token = await site.Token($"/ReturnRecord/Create?releaseRecordId={site.OnTimeRelease}");

            var response = await site.Client.PostAsync("/ReturnRecord/Create", site.Form(token, site.OnTimeRelease,
                DomainValues.ReturnConditions.Damaged, DomainValues.EquipmentStatuses.Available));
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Only items in Good condition can go back to Available.", WebUtility.HtmlDecode(html));
            Assert.Contains("ON-TIME", html);
        }

        [Fact]
        public async Task Unknown_loan_or_return_is_not_found()
        {
            await using var site = await ReturnHttpSite.CreateAsync();

            var missing = await site.Client.GetAsync("/ReturnRecord/Create?releaseRecordId=999");
            var details = await site.Client.GetAsync("/ReturnRecord/Details/999");

            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        }

        private sealed class ReturnHttpSite : IAsyncDisposable
        {
            private static readonly DateTime Now = DateTime.SpecifyKind(DateTime.UtcNow.AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute)), DateTimeKind.Utc);

            private SqliteDb _sqlite;
            private WebApplication _app;
            public HttpClient Client { get; private set; }
            public long OnTimeRelease { get; private set; }
            public long OverdueRelease { get; private set; }

            public static async Task<ReturnHttpSite> CreateAsync()
            {
                var site = new ReturnHttpSite { _sqlite = new SqliteDb() };
                try
                {
                    string custodianId;
                    using (var db = site._sqlite.Create())
                    {
                        custodianId = Staff(db, "CUS-HTTP", DomainValues.Roles.Custodian).Id;
                        site.OnTimeRelease = Loan(db, "ON-TIME", Now.AddDays(-1), Now.AddDays(1), Now.AddDays(-1)).ReleaseRecordId;
                        site.OverdueRelease = Loan(db, "OVERDUE", Now.AddDays(-3), Now.AddDays(-1), Now.AddDays(-3)).ReleaseRecordId;
                    }

                    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                    {
                        ApplicationName = typeof(ReturnRecordController).Assembly.GetName().Name,
                        EnvironmentName = "Testing"
                    });
                    builder.Logging.ClearProviders();
                    builder.WebHost.UseUrls("http://127.0.0.1:0");
                    builder.Services.AddDbContext<SqliteDashboardDbContext>(options => options.UseSqlite(site._sqlite.Connection));
                    builder.Services.AddScoped<AsiBasecodeDBContext>(provider => provider.GetRequiredService<SqliteDashboardDbContext>());
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
                    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
                    builder.Services.AddScoped<IReturnRecordRepository, ReturnRecordRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IEquipmentItemRepository, EquipmentItemRepository>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IReturnRecordService, ReturnRecordService>();
                    builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                    builder.Services.AddHttpContextAccessor();
                    builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ReturnRecordController).Assembly);
                    builder.Services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, ReturnTestAuthentication>("Test", _ => { });
                    builder.Services.AddAuthorization(options => options.AddPolicy(DomainValues.Permissions.TransactionReleaseReturn,
                        policy => policy.RequireClaim("permission", DomainValues.Permissions.TransactionReleaseReturn)));

                    site._app = builder.Build();
                    site._app.UseDeveloperExceptionPage();
                    site._app.UseRouting();
                    site._app.UseAuthentication();
                    site._app.UseAuthorization();
                    site._app.MapControllerRoute("default", "{controller=ReturnRecord}/{action=Index}/{id?}");
                    await site._app.StartAsync();

                    site.Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
                    {
                        BaseAddress = new Uri(site._app.Urls.Single())
                    };
                    site.Client.DefaultRequestHeaders.Add("X-Test-User", custodianId);
                    return site;
                }
                catch
                {
                    await site.DisposeAsync();
                    throw;
                }
            }

            public async Task<string> Html(string path)
            {
                var response = await Client.GetAsync(path);
                var html = await response.Content.ReadAsStringAsync();
                Assert.True(response.IsSuccessStatusCode, html);
                return html;
            }

            public async Task<string> Token(string path)
            {
                var html = await Html(path);
                var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
                Assert.NotEmpty(token);
                return WebUtility.HtmlDecode(token);
            }

            public FormUrlEncodedContent Form(string token, long releaseRecordId, string condition, string status) => new(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("ReleaseRecordId", releaseRecordId.ToString()),
                new KeyValuePair<string, string>("ActualReturnAt", ManilaClock.ToLocal(Now).ToString("yyyy-MM-ddTHH:mm")),
                new KeyValuePair<string, string>("ReturnedCondition", condition),
                new KeyValuePair<string, string>("ResultingItemStatus", status),
                new KeyValuePair<string, string>("Notes", "HTTP test")
            });

            public async ValueTask DisposeAsync()
            {
                Client?.Dispose();
                if (_app != null) await _app.DisposeAsync();
                _sqlite?.Dispose();
            }
        }

        public sealed class ReturnTestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public ReturnTestAuthentication(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder) : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (!Request.Headers.TryGetValue("X-Test-User", out var userId))
                    return Task.FromResult(AuthenticateResult.NoResult());

                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, DomainValues.Roles.Custodian),
                    new Claim("permission", DomainValues.Permissions.TransactionReleaseReturn)
                }, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
            }
        }
    }
}
