using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// WBS 39.00-42.00: the custodian approval-and-release workflow. Deliberately separate from
    /// ReservationController, which is Borrower-only for a borrower's own reservations.
    /// </summary>
    [Authorize(
        Roles = DomainValues.Roles.Custodian,
        Policy = DomainValues.Permissions.ReservationReview)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ReservationReviewController : ControllerBase<ReservationReviewController>
    {
        private readonly IReservationService _reservationService;

        public ReservationReviewController(
            IReservationService reservationService,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _reservationService = reservationService;
        }

        [HttpGet]
        public IActionResult Index(string status, string search, int page = 1)
        {
            return View(_reservationService.GetReviewQueue(status, search, page));
        }

        [HttpGet]
        public IActionResult Details(long id)
        {
            var model = _reservationService.RetrieveForReview(id);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(long id)
        {
            return Finish(_reservationService.ApproveReservation(UserId, id), id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(long id, RejectReservationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Give the borrower a reason for the rejection.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(_reservationService.RejectReservation(UserId, id, model?.Reason), id);
        }

        /// <summary>
        /// Records the handover. Requires transaction.release_return on top of the controller's
        /// reservation.review policy, so the two custodian duties stay separately grantable.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = DomainValues.Permissions.TransactionReleaseReturn)]
        public IActionResult Release(long id, ReleaseReservationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "The release notes are too long.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(_reservationService.ReleaseEquipment(UserId, id, model?.Notes), id);
        }

        private IActionResult Finish(ReservationResult result, long id)
        {
            if (result.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
