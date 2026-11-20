using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = DomainValues.Permissions.HistoryView)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class BorrowingHistoryController : ControllerBase<BorrowingHistoryController>
    {
        private readonly IBorrowingHistoryService _borrowingHistoryService;

        public BorrowingHistoryController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IBorrowingHistoryService borrowingHistoryService)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _borrowingHistoryService = borrowingHistoryService;
        }

        // WBS 52–54: binds the page filters, enforces the caller's history
        // scope, then asks the service for the filtered page model.
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] BorrowingHistoryFilterViewModel filter,
            CancellationToken cancellationToken)
        {
            // WBS 52 (view), 53 (search) and 54 (filter) share this GET action.
            // Views/BorrowingHistory/Index.cshtml submits query values into this model.
            filter ??= new BorrowingHistoryFilterViewModel();
            // This endpoint uses Search for equipment text; ignore the legacy Item field.
            filter.Item = null;
            ModelState.Remove(nameof(filter.Item));
            if (!TryGetHistoryScope(out var onlyOwnHistory))
            {
                return Forbid();
            }

            // The service scopes records first, combines filters, then returns one page.
            var model = await _borrowingHistoryService.GetHistoryAsync(
                filter,
                UserId,
                onlyOwnHistory,
                cancellationToken);
            if (!ModelState.IsValid)
            {
                // Binding errors (for example an invalid date) must not display a report
                // that looks as though the invalid filter was successfully applied.
                model.Items.Clear();
                model.TotalCount = 0;
                model.TotalPages = 1;
                model.Page = 1;
                model.ValidationMessage = "One or more filter values are invalid. Correct them and try again.";
            }

            SetPageData("Borrowing history");
            return View(model);
        }

        // WBS 55: exports reuse the validated filters and role scope from
        // Index, then stream service rows through the CSV formatter.
        [HttpGet]
        public async Task<IActionResult> Export(
            [FromQuery] BorrowingHistoryFilterViewModel filter,
            CancellationToken cancellationToken)
        {
            filter ??= new BorrowingHistoryFilterViewModel();
            filter.Item = null;
            ModelState.Remove(nameof(filter.Item));
            if (!TryGetHistoryScope(out var onlyOwnHistory))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest("One or more filter values are invalid.");
            }

            filter ??= new BorrowingHistoryFilterViewModel();
            var validationMessage = BorrowingHistoryService
                .NormalizeAndValidateFilter(filter);
            if (validationMessage != null)
            {
                return BadRequest(validationMessage);
            }

            // WBS 55: validate before starting a file response. Both export actions
            // use the same permitted scope and filter rules as the page above.
            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"borrowing-history-{ManilaClock.NowLocal:yyyyMMdd}.csv\"";

            // Write asynchronously for Kestrel; leave the response body owned by ASP.NET.
            await using var writer = new StreamWriter(
                Response.Body,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                1024,
                leaveOpen: true);
            await writer.WriteLineAsync(BorrowingHistoryCsvFormatter.FormatHeader());
            // StreamReportRowsAsync omits paging, so export includes ALL matching rows.
            // The formatter quotes values and protects spreadsheet-formula-like text.
            await foreach (var row in _borrowingHistoryService
                .StreamReportRowsAsync(
                    filter,
                    UserId,
                    onlyOwnHistory,
                    cancellationToken)
                .WithCancellation(cancellationToken))
            {
                await writer.WriteLineAsync(BorrowingHistoryCsvFormatter.FormatRow(row));
            }

            await writer.FlushAsync(cancellationToken);
            return new EmptyResult();
        }

        // WBS 55: XLSX uses the same scope and filtered row stream as CSV;
        // the exporter writes those rows and returns a temporary workbook stream.
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] BorrowingHistoryFilterViewModel filter,
            CancellationToken cancellationToken)
        {
            filter ??= new BorrowingHistoryFilterViewModel();
            filter.Item = null;
            ModelState.Remove(nameof(filter.Item));
            if (!TryGetHistoryScope(out var onlyOwnHistory))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest("One or more filter values are invalid.");
            }

            filter ??= new BorrowingHistoryFilterViewModel();
            var validationMessage = BorrowingHistoryService.NormalizeAndValidateFilter(filter);
            if (validationMessage != null)
            {
                return BadRequest(validationMessage);
            }

            var rows = _borrowingHistoryService.StreamReportRowsAsync(
                filter,
                UserId,
                onlyOwnHistory,
                cancellationToken);

            // XLSX writes those rows to a temporary file using the OpenXML exporter.
            // Its returned stream deletes the temporary file when the response closes it.
            FileStream workbook;
            try
            {
                workbook = await BorrowingHistoryXlsxExporter.CreateAsync(rows, cancellationToken);
            }
            catch (BorrowingHistoryWorkbookLimitException error)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, error.Message);
            }

            return File(
                workbook,
                BorrowingHistoryXlsxExporter.ContentType,
                $"borrowing-history-{ManilaClock.NowLocal:yyyyMMdd}.xlsx");
        }

        private bool TryGetHistoryScope(out bool onlyOwnHistory)
        {
            // The controller's history.view policy is required as well as this role check.
            // Borrower-only callers see their own records; staff roles allow wider scope.
            var isCustodian = User.IsInRole(DomainValues.Roles.Custodian);
            var isAdministrator = User.IsInRole(DomainValues.Roles.Administrator);
            var isBorrower = User.IsInRole(DomainValues.Roles.Borrower);

            onlyOwnHistory = isBorrower && !isCustodian && !isAdministrator;
            return isBorrower || isCustodian || isAdministrator;
        }

        private void SetPageData(string title)
        {
            ViewData["Title"] = title;
            ViewData["Eyebrow"] = "Reports";
        }
    }
}
