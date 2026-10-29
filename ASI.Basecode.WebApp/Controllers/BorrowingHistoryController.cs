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

        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] BorrowingHistoryFilterViewModel filter,
            CancellationToken cancellationToken)
        {
            if (!TryGetHistoryScope(out var onlyOwnHistory))
            {
                return Forbid();
            }

            var model = await _borrowingHistoryService.GetHistoryAsync(
                filter,
                UserId,
                onlyOwnHistory,
                cancellationToken);
            if (!ModelState.IsValid)
            {
                model.Items.Clear();
                model.TotalCount = 0;
                model.TotalPages = 1;
                model.Page = 1;
                model.ValidationMessage = "One or more filter values are invalid. Correct them and try again.";
            }

            SetPageData("Borrowing history");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(
            [FromQuery] BorrowingHistoryFilterViewModel filter,
            CancellationToken cancellationToken)
        {
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

            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"borrowing-history-{ManilaClock.NowLocal:yyyyMMdd}.csv\"";

            await using var writer = new StreamWriter(
                Response.Body,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                1024,
                leaveOpen: true);
            await writer.WriteLineAsync(BorrowingHistoryCsvFormatter.FormatHeader());
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

        private bool TryGetHistoryScope(out bool onlyOwnHistory)
        {
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
