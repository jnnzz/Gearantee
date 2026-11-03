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
            Assert.Contains("data-theme-picker", login);
            Assert.DoesNotContain("Create one", login);
            Assert.DoesNotContain("Remember me", login);
            Assert.Contains("name=\"RememberMe\" value=\"false\"", login);
            Assert.Contains("Email or school ID", login);
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
            Assert.Contains("type=\"datetime-local\"", create);
            using var admin = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            var users = await admin.GetStringAsync("/Users/Index");
            Assert.DoesNotContain("accounts-header-search", users);
            Assert.Single(Regex.Matches(users, "<input[^>]*name=\"search\""));
            Assert.DoesNotContain("input md:hidden", users);
            Assert.Contains("header-date", users);
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
