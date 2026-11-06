using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Tests.UiMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.Tests.Notifications
{
    public class NotificationHttpTests
    {
        [Fact]
        public async Task Failed_dashboards_show_retry_and_feedback_without_fake_statistics()
        {
            await using var site = await UiAcceptanceSite.CreateAsync(dashboardFails: true);
            foreach (var (id, role) in new[] { ("UI-BORROWER", "Borrower"), ("UI-CUST", "Custodian"), ("UI-ADMIN", "Administrator") })
            {
                using var client = site.NewClient();
                await site.LoginAsync(client, id);
                var html = await client.GetStringAsync("/Dashboard/" + role);
                Assert.Contains("Dashboard data is temporarily unavailable", html);
                Assert.Contains("data-flash-message=\"error\"", html);
                Assert.DoesNotContain("borrower-summary", html);
                Assert.DoesNotContain("role-summary", html);
                Assert.DoesNotContain("Injected dashboard failure", html);
            }
        }

        [Fact]
        public async Task School_id_login_recovery_and_otp_keep_email_server_side()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var client = site.NewClient();
            var login = await client.GetStringAsync("/Account/Login");
            Assert.Contains("data-force-light=\"true\"", login);
            Assert.DoesNotContain("data-theme-toggle", login);
            var failed = await Post(client, "/Account/Login", login, ("UserId", "borrower@ui-fixture.invalid"), ("Password", UiAcceptanceSite.Password));
            Assert.Contains("Invalid School ID or password.", await failed.Content.ReadAsStringAsync());
            var forgot = await client.GetStringAsync("/Account/ForgotPassword");
            Assert.DoesNotContain("type=\"email\"", forgot);
            var request = await Post(client, "/Account/ForgotPassword", forgot,
                ("SchoolId", "UI-BORROWER"), ("Email", "attacker@example.invalid"));
            Assert.Equal(HttpStatusCode.Redirect, request.StatusCode);
            Assert.Equal("borrower@ui-fixture.invalid", site.LastOtpEmail);
            var inbox = await client.GetStringAsync(request.Headers.Location);
            Assert.DoesNotContain("borrower@ui-fixture.invalid", inbox);
            Assert.DoesNotContain("name=\"Email\"", inbox);
            Assert.Contains("If this School ID", inbox);
            var verified = await Post(client, "/Account/VerifyPasswordResetOtp", inbox,
                ("SchoolId", "UI-BORROWER"), ("Otp", site.LastOtp));
            var reset = await verified.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            Assert.Contains("name=\"Token\"", reset);
            var reused = await Post(client, "/Account/VerifyPasswordResetOtp", inbox,
                ("SchoolId", "UI-BORROWER"), ("Otp", site.LastOtp));
            Assert.Contains("invalid or has expired", await reused.Content.ReadAsStringAsync());
            foreach (var id in new[] { "UNKNOWN-ID", "borrower@ui-fixture.invalid" })
            {
                request = await Post(client, "/Account/ForgotPassword", forgot, ("SchoolId", id));
                Assert.Equal(HttpStatusCode.Redirect, request.StatusCode);
                Assert.Contains("If this School ID", await client.GetStringAsync(request.Headers.Location));
            }
            Assert.Equal(1, site.OtpSendCount);
            using (var scope = site.App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                await db.Users.Where(x => x.UserName == "UI-CUST").ExecuteUpdateAsync(update => update.SetProperty(x => x.IsActive, false));
                await db.Users.Where(x => x.UserName == "UI-ADMIN").ExecuteUpdateAsync(update => update.SetProperty(x => x.Email, (string)null));
            }
            foreach (var id in new[] { "UI-CUST", "UI-ADMIN" })
            {
                request = await Post(client, "/Account/ForgotPassword", forgot, ("SchoolId", id));
                Assert.Equal(HttpStatusCode.Redirect, request.StatusCode);
                Assert.Contains("If this School ID", await client.GetStringAsync(request.Headers.Location));
            }
            Assert.Equal(1, site.OtpSendCount);
            Assert.Equal(HttpStatusCode.TooManyRequests,
                (await Post(client, "/Account/ForgotPassword", forgot, ("SchoolId", "UI-BORROWER"))).StatusCode);
            await site.LoginAsync(client, "UI-BORROWER");
        }

        [Fact]
        public async Task Inbox_endpoints_require_auth_and_antiforgery_and_never_update_another_users_rows()
        {
            await using var site = await UiAcceptanceSite.CreateAsync(notifications: true);
            using var borrower = site.NewClient();
            using var admin = site.NewClient();
            using var guest = site.NewClient();
            Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/Notification/Index")).StatusCode);
            await site.LoginAsync(borrower, "UI-BORROWER");
            await site.LoginAsync(admin, "UI-ADMIN");
            long id;
            using (var scope = site.App.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AsiBasecodeDBContext>();
                var notification = new Notification { UserId = site.BorrowerId, Type = "ReservationApproved",
                    Title = "Private <script>", Body = "Private borrower message", DedupKey = "http-test", CreatedAt = DateTime.UtcNow,
                    LinkUrl = "//evil.invalid" };
                db.Notifications.Add(notification); await db.SaveChangesAsync(); id = notification.NotificationId;
            }
            var html = await borrower.GetStringAsync("/Notification/Index");
            Assert.Contains("Private &lt;script&gt;", html);
            Assert.DoesNotContain("href=\"//evil.invalid", html);
            var adminHtml = await admin.GetStringAsync("/Notification/Index");
            Assert.DoesNotContain("Private borrower message", adminHtml);
            Assert.Equal(HttpStatusCode.BadRequest, (await borrower.PostAsync("/Notification/MarkRead",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = id.ToString() }))).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, "/Notification/MarkRead", adminHtml, ("id", id.ToString()))).StatusCode);
            Assert.Contains("\"count\":1", await borrower.GetStringAsync("/Notification/UnreadCount"));
            await Post(borrower, "/Notification/MarkRead", html, ("id", id.ToString()));
            Assert.Contains("\"count\":0", await borrower.GetStringAsync("/Notification/UnreadCount"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await borrower.GetAsync("/Notification/MarkAllRead")).StatusCode);
        }

        private static Task<HttpResponseMessage> Post(HttpClient client, string url, string html, params (string Key, string Value)[] fields)
        {
            var form = fields.ToDictionary(x => x.Key, x => x.Value);
            form["__RequestVerificationToken"] = UiAcceptanceSite.Token(html);
            return client.PostAsync(url, new FormUrlEncodedContent(form));
        }
    }
}
