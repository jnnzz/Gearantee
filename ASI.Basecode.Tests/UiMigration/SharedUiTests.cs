using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.UiMigration
{
    public class SharedUiTests
    {
        [Fact]
        public async Task Refinement_tabs_actions_and_filters_render_for_the_correct_roles()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            using var custodian = site.NewClient();
            using var borrower = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            await site.LoginAsync(custodian, "UI-CUST");
            await site.LoginAsync(borrower, "UI-BORROWER");
            var inventory = await admin.GetStringAsync("/Categories/Index?tab=equipment");
            Assert.Matches("<section[^>]*hidden=\"hidden\"[^>]*aria-labelledby=\"categories-heading\"", inventory);
            Assert.DoesNotMatch("<section[^>]*hidden[^>]*aria-labelledby=\"equipment-heading\"", inventory);
            var categories = await admin.GetStringAsync("/Categories/Index?tab=categories");
            Assert.Matches("<section[^>]*hidden=\"hidden\"[^>]*aria-labelledby=\"equipment-heading\"", categories);
            var create = await admin.GetStringAsync("/Categories/CreateEquipment");
            Assert.DoesNotContain("name=\"ItemCode\"", create);
            var dashboard = await custodian.GetStringAsync("/Dashboard/Custodian");
            var shortcuts = Regex.Match(dashboard, "<section class=\"surface shortcut-card\">[\\s\\S]*?</section>").Value;
            Assert.Contains("href=\"/Catalog", shortcuts);
            Assert.Contains("href=\"/BorrowingHistory", shortcuts);
            Assert.DoesNotContain("Soon", shortcuts);
            var review = await custodian.GetStringAsync("/ReservationReview/Index");
            Assert.True(review.IndexOf("aria-label=\"Filter by status\"", StringComparison.Ordinal) < review.IndexOf("id=\"review-search\"", StringComparison.Ordinal));
            var history = await borrower.GetStringAsync("/Reservation/Index?search=NONEXISTENT");
            Assert.Contains("No matching reservations", history);
            Assert.Contains("value=\"NONEXISTENT\"", history);
            var report = await admin.GetStringAsync("/BorrowingHistory/Index?Item=NONEXISTENT");
            Assert.DoesNotContain("id=\"history-item\"", report);
            Assert.Contains("id=\"history-from\"", report);
            Assert.Contains("id=\"history-to\"", report);
            Assert.DoesNotContain("Download Excel", report);
        }

        [Fact]
        public async Task Required_labels_render_one_reusable_marker_in_manual_and_automatic_forms()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var anonymous = site.NewClient();
            using var admin = site.NewClient();
            using var custodian = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            await site.LoginAsync(custodian, "UI-CUST");
            var cases = new[]
            {
                (anonymous, "/Account/Login", "Password"),
                (admin, "/Categories/CreateEquipment", "ItemName"),
                (admin, "/Users/Create", "Password"),
                (custodian, $"/ReturnRecord/Create?releaseRecordId={site.ReleaseId}", "ActualReturnAt"),
                (custodian, $"/ReturnRecord/Create?releaseRecordId={site.ReleaseId}", "Notes"),
                (custodian, $"/ReservationReview/Details?id={site.PendingId}", "reject-reason")
            };
            foreach (var (client, path, field) in cases)
            {
                var html = await client.GetStringAsync(path);
                var labels = Regex.Matches(html, $"<label\\b[^>]*for=\"{field}\"[^>]*>([\\s\\S]*?)</label>");
                var label = Assert.Single(labels.Cast<Match>()).Value;
                var markers = Regex.Matches(label, "<span[^>]*class=\"required-marker\"[^>]*>");
                var marker = Assert.Single(markers.Cast<Match>()).Value;
                Assert.Contains("data-required-marker", marker);
            }
        }

        [Fact]
        public async Task Late_and_error_badges_and_destructive_actions_do_not_use_the_heading_color_as_a_fill()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var custodian = site.NewClient();
            await site.LoginAsync(custodian, "UI-CUST");
            var history = await custodian.GetStringAsync("/BorrowingHistory/Index");
            Assert.Matches("<span class=\"status-badge status-danger\">Yes · \\d+ days?</span>", history);
            var deniedResponse = await custodian.GetAsync("/Account/AccessDenied");
            Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
            var denied = await deniedResponse.Content.ReadAsStringAsync();
            Assert.Contains("<span class=\"status-badge status-danger\">403</span>", denied);
            var review = await custodian.GetStringAsync($"/ReservationReview/Details?id={site.PendingId}");
            Assert.Matches("<button[^>]*class=\"btn-danger\"[^>]*>Reject request</button>", review);
            using var borrower = site.NewClient();
            await site.LoginAsync(borrower, "DEMO-001");
            var ownRequest = await borrower.GetStringAsync($"/Reservation/Details?id={site.PendingId}");
            Assert.Matches("<button[^>]*class=\"btn-danger\"[^>]*>Cancel reservation</button>", ownRequest);
        }

        [Fact]
        public async Task Login_and_role_shell_keep_authentication_and_navigation_contracts()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var borrower = site.NewClient();
            var login = await borrower.GetStringAsync("/Account/Login");
            Assert.Contains("campus-equipment.svg", login);
            Assert.DoesNotContain("data-theme-toggle", login);
            Assert.DoesNotContain("data-theme-picker", login);
            Assert.DoesNotContain("Create one", login);
            Assert.DoesNotContain("Remember me", login);
            Assert.Contains("name=\"RememberMe\" value=\"false\"", login);
            Assert.Contains("School ID", login);
            Assert.DoesNotContain("Email or school ID", login);
            Assert.Contains("data-required-marker", login);
            Assert.Contains("theme.js", login);

            var signedIn = await borrower.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["UserId"] = "UI-BORROWER", ["Password"] = UiAcceptanceSite.Password,
                ["__RequestVerificationToken"] = UiAcceptanceSite.Token(login)
            }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            var authCookie = signedIn.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("Gearantee.UiAcceptance."));
            Assert.DoesNotContain("expires=", authCookie.ToLowerInvariant());
            var dashboard = await borrower.GetStringAsync("/Dashboard/Borrower");
            var sidebar = Regex.Match(dashboard, "<aside id=\"app-sidebar\".*?</aside>", RegexOptions.Singleline).Value;
            Assert.Contains("My borrowing history", sidebar);
            Assert.DoesNotContain("New reservation", sidebar);
            Assert.DoesNotContain("My reservations", sidebar);
            Assert.DoesNotContain("header-date", dashboard);
            var create = await borrower.GetStringAsync($"/Reservation/Create?equipmentId={site.AvailableItemId}");
            Assert.Matches("<a(?=[^>]*href=\"/Catalog)(?=[^>]*aria-current=\"page\")[^>]*>", create);
            Assert.DoesNotContain("type=\"datetime-local\"", create);
            Assert.Contains("type=\"date\"", create);
            Assert.Contains("data-theme-toggle", dashboard);
            using var admin = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            var users = await admin.GetStringAsync("/Users/Index");
            Assert.DoesNotContain("accounts-header-search", users);
            Assert.Single(Regex.Matches(users, "<input[^>]*name=\"search\""));
            Assert.DoesNotContain("input md:hidden", users);
            Assert.Contains("header-date", users);
        }

        [Fact]
        public async Task Catalog_discards_legacy_date_filters_and_calendar_accepts_equipment_search()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var borrower = site.NewClient();
            await site.LoginAsync(borrower, "UI-BORROWER");

            var redirect = await borrower.GetAsync("/Catalog/Index?Search=demo&page=3&From=bad&To=2026-11-03");
            Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
            var location = redirect.Headers.Location.ToString();
            Assert.Contains("Search=demo", location, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("From=", location, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("page=", location, StringComparison.OrdinalIgnoreCase);
            var catalog = await borrower.GetStringAsync(redirect.Headers.Location);
            Assert.DoesNotContain("datetime-local", catalog);

            var calendar = await borrower.GetStringAsync("/AvailabilityCalendar/Index?Search=DEMO&View=overview");
            Assert.Contains("id=\"availability-overview\"", calendar);
            Assert.Contains("value=\"DEMO\"", calendar);
            var oldView = await borrower.GetStringAsync("/AvailabilityCalendar/Index?View=timeGridDay");
            Assert.Contains("value=\"timeGridDay\"", oldView);
            Assert.Contains("id=\"availability-overview\"", oldView);
            Assert.DoesNotContain("id=\"availability-location\"", oldView);
            Assert.DoesNotContain("id=\"availability-equipment\"", oldView);
            var invalid = await borrower.GetAsync($"/AvailabilityCalendar/Events?Search={new string('x', 101)}&start=2026-11-02T00:00:00Z&end=2026-11-03T00:00:00Z");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        [Fact]
        public async Task Flash_messages_are_encoded_once_then_consumed_for_viewdata_and_tempdata_flows()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            var form = await admin.GetStringAsync("/Categories/Create");
            var save = await admin.PostAsync("/Categories/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["CategoryCode"] = "UI-FLASH", ["CategoryName"] = "<strong>Camera & gear</strong>",
                ["IsActive"] = "true", ["__RequestVerificationToken"] = UiAcceptanceSite.Token(form)
            }));
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
            var saved = await admin.GetStringAsync(save.Headers.Location);
            Assert.Single(Regex.Matches(saved, "data-flash-message=\"success\""));
            Assert.Contains("&lt;strong&gt;Camera &amp; gear&lt;/strong&gt;", saved);
            Assert.DoesNotContain("<strong>Camera & gear</strong>", saved);
            Assert.DoesNotContain("data-flash-message=\"success\"", await admin.GetStringAsync(save.Headers.Location));

            using var custodian = site.NewClient();
            await site.LoginAsync(custodian, "UI-CUST");
            var details = await custodian.GetStringAsync($"/ReservationReview/Details?id={site.PendingId}");
            Assert.Contains("btn-primary", Regex.Match(details, "<dialog id=\"approve-reservation-dialog\".*?</dialog>", RegexOptions.Singleline).Value);
            Assert.Contains("btn-danger", Regex.Match(details, "<dialog id=\"reject-reservation-dialog\".*?</dialog>", RegexOptions.Singleline).Value);
            var reject = await custodian.PostAsync("/ReservationReview/Reject", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = site.PendingId.ToString(), ["Reason"] = "",
                ["__RequestVerificationToken"] = UiAcceptanceSite.Token(details)
            }));
            Assert.Equal(HttpStatusCode.Redirect, reject.StatusCode);
            var failed = await custodian.GetStringAsync(reject.Headers.Location);
            Assert.Single(Regex.Matches(failed, "data-flash-message=\"error\""));
            Assert.Contains("Give the borrower a reason for the rejection.", failed);
            Assert.DoesNotContain("data-flash-message=\"error\"", await custodian.GetStringAsync(reject.Headers.Location));
        }
    }
}
