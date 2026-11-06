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
        public async Task<IActionResult> Index(string search, int page = 1)
        {
            var model = await _borrowerProfileService.GetBorrowerProfilesAsync(search, page);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
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
            var result = await _borrowerProfileService.UpdateEligibilityAsync(UserId, id, isEligible);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private void LoadBorrowerDisplay(BorrowerProfileViewModel borrower)
        {
            ViewData["BorrowerProfileId"] = borrower.BorrowerProfileId;
            ViewData["SchoolId"] = borrower.SchoolId;
            ViewData["BorrowerName"] = borrower.BorrowerName;
            ViewData["IsEligible"] = borrower.IsEligible;
        }
    }
}
