using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Returns;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>Return and condition check (WBS 44.00-47.00) for custodians and administrators.</summary>
    [Authorize(
        Roles = DomainValues.Roles.Custodian + "," + DomainValues.Roles.Administrator,
        Policy = DomainValues.Permissions.TransactionReleaseReturn)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ReturnRecordController : ControllerBase<ReturnRecordController>
    {
        private readonly IReturnRecordService _returnRecordService;

        /// <summary>Constructor</summary>
        public ReturnRecordController(IHttpContextAccessor httpContextAccessor,
                                      ILoggerFactory loggerFactory,
                                      IConfiguration configuration,
                                      IReturnRecordService returnRecordService) : base(httpContextAccessor, loggerFactory, configuration)
        {
            _returnRecordService = returnRecordService;
        }

        // GET: /ReturnRecord?tab=Overdue&page=1
        [HttpGet]
        public IActionResult Index(string tab, string search, int page = 1)
        {
            // Cataraja: WBS 44/47: ReturnRecordService supplies the searched/paged
            // Cataraja: On loan, Overdue, Returned and Late returns tabs for ReturnRecord/Index.
            var model = _returnRecordService.GetReturnRecords(tab, page, search);
            SetPageData("Return & condition");
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View(model);
        }

        // GET: /ReturnRecord/Details/5
        [HttpGet]
        public IActionResult Details(long id, string tab = null, string search = null, int page = 1)
        {
            // Cataraja: WBS 44-47: display the saved inspection, receiver and late data;
            // Cataraja: keep list context so the back link returns to the selected tab/search/page.
            var returnRecord = _returnRecordService.RetrieveReturnRecord(id);
            if (returnRecord == null) return NotFound();

            ViewData["ReturnTab"] = string.IsNullOrWhiteSpace(tab) ? "Returned" : tab;
            ViewData["ReturnSearch"] = search ?? string.Empty;
            ViewData["ReturnPage"] = Math.Max(page, 1);
            SetPageData($"Return Â· {returnRecord.ReservationCode}");
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            return View(returnRecord);
        }

        // GET: /ReturnRecord/Create?releaseRecordId=5
        [HttpGet]
        public IActionResult Create(
            long releaseRecordId,
            string tab = null,
            string search = null,
            int page = 1)
        {
            // Cataraja: WBS 44 GET: retrieve an open loan and prefill Manila return time,
            // Cataraja: Good condition and its default status for ReturnRecord/Create.cshtml.
            var loan = _returnRecordService.RetrieveActiveLoan(releaseRecordId);
            if (loan == null) return NotFound();

            var model = new ReturnRecordViewModel
            {
                ReleaseRecordId = releaseRecordId,
                ActualReturnAt = TruncateToMinute(ManilaClock.NowLocal),
                ReturnedCondition = DomainValues.ReturnConditions.Good,
                ResultingItemStatus = ReturnInspection.DefaultStatusFor(DomainValues.ReturnConditions.Good),
                Loan = loan
            };

            SetReturnListContext(tab, search, page);
            SetPageData("Record a return");
            return View(model);
        }

        // POST: /ReturnRecord/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            ReturnRecordViewModel model,
            string tab,
            string search,
            int page = 1)
        {
            // Cataraja: WBS 44-47 POST: the service validates inspection/time/permission,
            // Cataraja: then saves return, optional lateness and equipment condition/status together.
            if (!ModelState.IsValid)
            {
                return RedisplayCreate(model, tab, search, page);
            }

            var result = _returnRecordService.AddReturnRecord(model, UserId);
            if (result.NotFound) return NotFound();

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return RedisplayCreate(model, tab, search, page);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new
            {
                id = result.ReturnRecordId,
                tab = "Returned",
                search,
                page = 1
            });
        }

        // Loan may have been returned meanwhile; send back to the list
        private IActionResult RedisplayCreate(
            ReturnRecordViewModel model,
            string tab,
            string search,
            int page)
        {
            // Cataraja: WBS 58 (shared UI): preserve entered values and list context
            // Cataraja: after validation failure, but reload the loan in case another return won.
            model.Loan = _returnRecordService.RetrieveActiveLoan(model.ReleaseRecordId);
            if (model.Loan == null)
            {
                TempData["ErrorMessage"] = "That loan is no longer open. It may have just been returned.";
                return RedirectToAction(nameof(Index), new { tab, search, page });
            }

            SetReturnListContext(tab, search, page);
            SetPageData("Record a return");
            return View(nameof(Create), model);
        }

        private void SetReturnListContext(string tab, string search, int page)
        {
            ViewData["ReturnTab"] = string.IsNullOrWhiteSpace(tab) ? "OnLoan" : tab;
            ViewData["ReturnSearch"] = search ?? string.Empty;
            ViewData["ReturnPage"] = Math.Max(page, 1);
        }

        private static DateTime TruncateToMinute(DateTime value)
        {
            return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
        }

        private void SetPageData(string title)
        {
            ViewData["Title"] = title;
            ViewData["Eyebrow"] = User.IsInRole(DomainValues.Roles.Administrator) && !User.IsInRole(DomainValues.Roles.Custodian)
                ? "Administrator workspace"
                : "Custodian workspace";
        }
    }
}
