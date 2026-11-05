using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xunit;

namespace ASI.Basecode.Tests.UiMigration
{
    public class UiAcceptanceTests
    {
        [Fact]
        public async Task All_role_screens_render_through_real_MVC_with_existing_permission_boundaries()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            using var custodian = site.NewClient();
            using var borrower = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            await site.LoginAsync(custodian, "UI-CUST");
            await site.LoginAsync(borrower, "UI-BORROWER");
            var adminPaths = new[]
            {
                "/Dashboard/Administrator", "/Categories/Index", "/Categories/Index?tab=categories", "/Categories/Create",
                $"/Categories/Edit?id={site.CategoryId}", "/Categories/CreateEquipment",
                $"/Categories/EditEquipment?id={site.AvailableItemId}", $"/Categories/EditEquipment?id={site.LockedItemId}",
                "/BorrowerProfile/Index", $"/BorrowerProfile/Edit?id={site.BorrowerProfileId}",
                "/Users/Index", "/Users/Create", $"/Users/Edit?id={site.BorrowerId}", "/Users/Import",
                "/BorrowingHistory/Index", "/AvailabilityCalendar/Index"
            };
            var custodianPaths = new[]
            {
                "/Dashboard/Custodian", "/ReservationReview/Index", "/ReservationReview/Index?status=Pending", "/ReservationReview/Index?status=Approved",
                "/ReservationReview/Index?status=Rejected", "/ReservationReview/Index?status=All",
                "/ReservationReview/Index?status=Cancelled", "/ReservationReview/Index?status=Expired",
                $"/ReservationReview/Details?id={site.PendingId}", $"/ReservationReview/Details?id={site.PickupId}",
                $"/ReservationReview/Details?id={site.RejectedId}", "/ReturnRecord/Index", "/ReturnRecord/Index?tab=Overdue",
                "/ReturnRecord/Index?tab=Returned", "/ReturnRecord/Index?tab=Late",
                $"/ReturnRecord/Create?releaseRecordId={site.ReleaseId}", $"/ReturnRecord/Details?id={site.ReturnedId}",
                "/BorrowingHistory/Index", "/AvailabilityCalendar/Index"
            };
            foreach (var path in adminPaths) await Check(admin, path);

            var adminDashboardHtml = await Check(admin, "/Dashboard/Administrator");
            Assert.Contains("admin-dashboard-caption", adminDashboardHtml);
            Assert.Contains("Inventory by category", adminDashboardHtml);
            Assert.DoesNotContain("Shortcuts", adminDashboardHtml);
            var equipmentManagementHtml = await Check(admin, "/Categories/Index?tab=categories");
            Assert.Contains("Equipment &amp; categories", equipmentManagementHtml);
            var createEquipmentHtml = await Check(admin, "/Categories/CreateEquipment");
            Assert.Contains("data-clear-image", createEquipmentHtml);

            var userEditHtml = await Check(admin, $"/Users/Edit?id={site.BorrowerId}");
            Assert.Contains("class=\"btn-danger\"", userEditHtml);
            Assert.Contains("class=\"status-badge status-success\">Active</span>", userEditHtml);
            var usersIndexHtml = await Check(admin, "/Users/Index");
            Assert.Single(Regex.Matches(usersIndexHtml, "id=\"user-search\" name=\"search\""));
            var topbar = Regex.Match(usersIndexHtml, "<header class=\"workspace-topbar\">.*?</header>", RegexOptions.Singleline);
            Assert.True(topbar.Success);
            Assert.DoesNotContain("type=\"search\"", topbar.Value);
            Assert.Single(Regex.Matches(usersIndexHtml, "<h1\\b", RegexOptions.IgnoreCase));
            var appStyles = await admin.GetStringAsync("/css/app.css");
            Assert.Contains(".ui-refresh .text-link{color:var(--ui-link)}", appStyles);
            Assert.Contains("--ui-link:#a8cff4", appStyles);
            Assert.Contains(".ui-refresh .status-badge.status-success{background:var(--ui-success-bg);color:var(--ui-success-text)}", appStyles);
            Assert.Contains(".ui-refresh .status-badge.status-danger{background:var(--ui-danger-bg);color:var(--ui-danger-text)}", appStyles);
            Assert.Contains(".ui-refresh .btn-danger", appStyles);
            Assert.Contains("--ui-danger-text:#ffb8b3", appStyles);
            Assert.Contains(".ui-refresh :is(a,button):focus-visible", appStyles);
            Assert.Contains(".ui-refresh .input:focus{border-color:var(--ui-link);outline:2px solid var(--ui-link);outline-offset:1px}", appStyles);
            Assert.Contains(".ui-refresh .user-filters{grid-template-columns:minmax(180px,1.5fr) repeat(2,minmax(120px,1fr)) auto}", appStyles);
            Assert.Contains("@media (max-width:767px)", appStyles);
            Assert.Contains(".ui-refresh .user-filters>input{grid-column:1/-1}", appStyles);
            Assert.Contains("@media (max-width:599px)", appStyles);
            Assert.Contains(".ui-refresh .user-filters{grid-template-columns:minmax(0,1fr)}", appStyles);
            var inventoryStyles = await admin.GetStringAsync("/css/admin-inventory.css");
            Assert.Contains(".equipment-image-upload:focus-within { outline: 3px solid var(--ui-link); outline-offset: 3px; }", inventoryStyles);
            Assert.Contains(".admin-operations-grid { align-items: stretch; }", inventoryStyles);
            Assert.Contains("repeat(auto-fit, minmax(min(100%, 150px), 1fr))", inventoryStyles);

            foreach (var path in custodianPaths) await Check(custodian, path);
            var reviewSearch = await Check(custodian, "/ReservationReview/Index?search=NO_MATCH_UI_FIXTURE");
            Assert.Contains("No matching requests", reviewSearch);
            Assert.Single(Regex.Matches(reviewSearch, "<h1\\b", RegexOptions.IgnoreCase));
            var returnSearch = await Check(custodian, "/ReturnRecord/Index?tab=Overdue&search=NO_MATCH_UI_FIXTURE");
            Assert.Contains("No matching records", returnSearch);
            Assert.Contains("name=\"tab\" value=\"Overdue\"", returnSearch);
            Assert.Single(Regex.Matches(returnSearch, "<h1\\b", RegexOptions.IgnoreCase));
            var historyHtml = await Check(admin, "/BorrowingHistory/Index?Search=NO_MATCH_UI_FIXTURE");
            Assert.Contains("More filters", historyHtml);
            Assert.Contains("Download Excel", historyHtml);
            Assert.Contains("Download CSV", historyHtml);
            Assert.Single(Regex.Matches(historyHtml, "<h1\\b", RegexOptions.IgnoreCase));
            foreach (var path in new[] { "/Dashboard/Borrower", "/Catalog/Index", $"/Catalog/Details?id={site.AvailableItemId}", "/Reservation/Index", $"/Reservation/Create?equipmentId={site.AvailableItemId}", "/AvailabilityCalendar/Index" })
                await Check(borrower, path);
            foreach (var client in new[] { custodian, borrower })
            {
                var denied = await client.GetAsync("/Users/Index");
                Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
                Assert.Contains("AccessDenied", denied.Headers.Location.ToString());
            }
        }

        // Regression coverage for asynchronous disposal of the streamed CSV response.
        [Fact]
        public async Task Csv_export_completes_under_default_Kestrel_response_rules()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            System.Net.Http.HttpResponseMessage csv;
            try { csv = await admin.GetAsync("/BorrowingHistory/Export"); }
            catch (System.Net.Http.HttpRequestException error)
            {
                throw new InvalidOperationException("CSV server failure: " + site.LastRequestFailure, error);
            }
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Contains("text/csv", csv.Content.Headers.ContentType.ToString());
            Assert.Equal("attachment", csv.Content.Headers.ContentDisposition.DispositionType);
            Assert.StartsWith("borrowing-history-", csv.Content.Headers.ContentDisposition.FileName.Trim('"'));
            var content = await csv.Content.ReadAsStringAsync();
            Assert.StartsWith(ASI.Basecode.Services.Utilities.BorrowingHistoryCsvFormatter.FormatHeader(), content);
            Assert.Contains("DEMO-LAP-001", content);
        }

        [Fact]
        public async Task Excel_export_is_a_readable_attachment_and_uses_the_requested_report_filter()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");

            using var response = await admin.GetAsync("/BorrowingHistory/ExportExcel?Search=DEMO-LAP-001");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                response.Content.Headers.ContentType.MediaType);
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition.DispositionType);
            Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition.FileName.Trim('"'));

            var bytes = await response.Content.ReadAsByteArrayAsync();
            using var stream = new MemoryStream(bytes);
            using var workbook = SpreadsheetDocument.Open(stream, false);
            var workbookPart = workbook.WorkbookPart;
            var sheet = Assert.Single(workbookPart.Workbook.Sheets.Elements<Sheet>());
            var worksheet = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
            var rows = worksheet.Worksheet.Elements<SheetData>().Single().Elements<Row>().ToArray();

            Assert.Equal("Reservation", rows[0].Elements<Cell>().First().InlineString.InnerText);
            Assert.True(rows.Length > 1);
            Assert.All(rows.Skip(1), row =>
                Assert.Contains("DEMO-LAP-001", string.Join(" ", row.Elements<Cell>().Select(cell => cell.InlineString?.InnerText))));
        }

        [Fact]
        public async Task Empty_filters_and_invalid_forms_keep_existing_validation_and_redisplay_context()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            using var admin = site.NewClient();
            using var custodian = site.NewClient();
            await site.LoginAsync(admin, "UI-ADMIN");
            await site.LoginAsync(custodian, "UI-CUST");
            foreach (var entry in new[]
            {
                ("/BorrowerProfile/Index?search=NO_MATCH_UI_FIXTURE", "No borrower profiles match that search."),
                ("/Categories/Index?equipmentSearch=NO_MATCH_UI_FIXTURE", "No equipment items match these filters."),
                ("/Users/Index?search=NO_MATCH_UI_FIXTURE", "No accounts match these filters."),
                ("/BorrowingHistory/Index?Search=NO_MATCH_UI_FIXTURE", "No borrowing history found")
            }) Assert.Contains(entry.Item2, await Check(admin, entry.Item1));
            Assert.Contains("No matching requests", await Check(custodian, "/ReservationReview/Index?search=NO_MATCH_UI_FIXTURE"));
            await Invalid(admin, "/Categories/Create", new() { ["CategoryCode"] = "", ["CategoryName"] = "" });
            await Invalid(admin, "/Users/Create", new() { ["RoleName"] = "Custodian", ["UserCode"] = "", ["FirstName"] = "", ["LastName"] = "", ["Email"] = "", ["Password"] = "", ["ConfirmPassword"] = "" });
            var profilePath = $"/BorrowerProfile/Edit?id={site.BorrowerProfileId}";
            var profileHtml = await admin.GetStringAsync(profilePath);
            var stamp = Regex.Match(profileHtml, "name=\"ConcurrencyStamp\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
            await Invalid(admin, profilePath, new() { ["id"] = site.BorrowerProfileId.ToString(), ["ConcurrencyStamp"] = WebUtility.HtmlDecode(stamp), ["Department"] = "" });
            await Invalid(custodian, $"/ReturnRecord/Create?releaseRecordId={site.ReleaseId}", new() { ["ReleaseRecordId"] = site.ReleaseId.ToString(), ["ActualReturnAt"] = "", ["ReturnedCondition"] = "", ["ResultingItemStatus"] = "" });
        }

        [InteractiveUiPreviewFact]
        public async Task Interactive_ui_preview_uses_only_disposable_fixture_data()
        {
            await using var site = await UiAcceptanceSite.CreateAsync();
            var stop = Path.Combine(site.Root, "artifacts/ui-acceptance-stop");
            var until = DateTime.UtcNow.AddMinutes(40);
            Console.WriteLine("UI acceptance preview: http://127.0.0.1:5192");
            while (!File.Exists(stop) && DateTime.UtcNow < until) await Task.Delay(1000);
        }

        private static async Task Invalid(HttpClient client, string path, Dictionary<string, string> fields)
        {
            var before = await client.GetStringAsync(path);
            fields["__RequestVerificationToken"] = UiAcceptanceSite.Token(before);
            var response = await client.PostAsync(path, new FormUrlEncodedContent(fields));
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Matches("class=\"[^\"]*field-validation-error[^\"]*\"[^>]*>\\s*[^<\\s]", html);
            Assert.NotEmpty(UiAcceptanceSite.Token(html));
        }

        private static async Task<string> Check(System.Net.Http.HttpClient client, string path)
        {
            var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            var safeError = html.Split("HEADERS")[0];
            Assert.True(response.StatusCode == HttpStatusCode.OK, path + ": " + response.StatusCode + " " + safeError[..Math.Min(safeError.Length, 1800)]);
            Assert.Contains("main-content", html);
            Assert.DoesNotContain("Dashboard information could not be loaded", html);
            Assert.DoesNotContain("<title> · Gearantee</title>", html);
            return html;
        }
    }

    public sealed class InteractiveUiPreviewFactAttribute : FactAttribute
    {
        public InteractiveUiPreviewFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("GEARANTEE_UI_PREVIEW") != "1")
                Skip = "Opt-in interactive browser fixture; set GEARANTEE_UI_PREVIEW=1 to run.";
        }
    }
}
