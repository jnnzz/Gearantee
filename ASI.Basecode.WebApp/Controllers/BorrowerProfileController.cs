using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(
        Roles = DomainValues.Roles.Administrator,
        Policy = DomainValues.Permissions.BorrowerManage)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class BorrowerProfileController : ControllerBase<BorrowerProfileController>
    {
        // Canencia: WBS 30-32 flow: BorrowerProfile views -> this controller -> BorrowerProfileService.
        // Canencia: The class requires Administrator + borrower.manage; write services recheck access.
        private readonly IBorrowerProfileService _borrowerProfileService;

        public BorrowerProfileController(
            IBorrowerProfileService borrowerProfileService,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _borrowerProfileService = borrowerProfileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string search, string eligibility, int page = 1)
        {
            // Canencia: WBS 30: translate the dropdown value into an optional boolean filter.
            // Canencia: Null means all profiles; the service combines it with search and paging.
            bool? isEligible = eligibility?.Trim().ToLowerInvariant() switch
            {
                "eligible" => true,
                "ineligible" => false,
                _ => null
            };

            var model = await _borrowerProfileService.GetBorrowerProfilesAsync(search, page, isEligible);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            // Canencia: WBS 31 GET: load the current record for Views/BorrowerProfile/Edit.cshtml.
            // Canencia: Carry its concurrency stamp into the form to detect a newer save on POST.
            var borrower = await _borrowerProfileService.GetBorrowerProfileAsync(id);
            if (borrower == null)
            {
                return NotFound();
            }

            LoadBorrowerDisplay(borrower);

            return View(new BorrowerProfileUpdateViewModel
            {
                Department = borrower.Department,
                ContactNumber = borrower.ContactNumber,
                ConcurrencyStamp = borrower.ConcurrencyStamp
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, BorrowerProfileUpdateViewModel model)
        {
            // Canencia: WBS 31 POST: annotations validate department/contact inputs first.
            // Canencia: On invalid input, redisplay the submitted values with borrower display data.
            if (!ModelState.IsValid)
            {
                var borrower = await _borrowerProfileService.GetBorrowerProfileAsync(id);
                ViewData["BorrowerProfileId"] = id;
                ViewData["SchoolId"] = borrower?.SchoolId;
                ViewData["BorrowerName"] = borrower?.BorrowerName;
                ViewData["IsEligible"] = borrower?.IsEligible ?? false;

                // The posted stamp is kept so the corrected form can still be saved.
                return View(model);
            }

            // Canencia: The service rechecks persisted permissions and the posted stamp, then
            // Canencia: saves the editable fields and audit event in the same transaction.
            var result = await _borrowerProfileService.UpdateBorrowerProfileAsync(UserId, id, model);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            if (result.IsStale)
            {
                // Reload the current values rather than overwriting the other administrator's save.
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Edit), new { id });
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEligibility(long id, bool isEligible)
        {
            // Canencia: WBS 32: post the desired eligibility value, not an instruction to toggle.
            // Canencia: The service handles repeated submissions and records the eligibility audit.
            var result = await _borrowerProfileService.UpdateEligibilityAsync(UserId, id, isEligible);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private void LoadBorrowerDisplay(BorrowerProfileViewModel borrower)
        {
            // Canencia: Provide read-only identity/status labels separately from the editable fields.
            ViewData["BorrowerProfileId"] = borrower.BorrowerProfileId;
            ViewData["SchoolId"] = borrower.SchoolId;
            ViewData["BorrowerName"] = borrower.BorrowerName;
            ViewData["IsEligible"] = borrower.IsEligible;
        }
    }
}
