using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
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
            // Canencia: WBS 39: Views/ReservationReview/Index.cshtml submits queue filters here.
            // Canencia: ReservationService returns counts, one page of requests and action flags.
            return View(_reservationService.GetReviewQueue(status, search, page));
        }

        [HttpGet]
        public IActionResult Details(long id)
        {
            // Canencia: WBS 39: load borrower/item/schedule and decision/release information for
            // Canencia: Details.cshtml; this page posts Approve, Reject or Release to this controller.
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
            // Canencia: WBS 40: the service rechecks the actor, locks the equipment and validates
            // Canencia: current request state/availability before recording the approval and audit.
            return Finish(_reservationService.ApproveReservation(UserId, id), id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(long id, RejectReservationViewModel model)
        {
            // Canencia: WBS 41: require a reason in the form and service. A rejected request keeps
            // Canencia: that reason for borrower visibility rather than deleting the request record.
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
            // Canencia: WBS 42: require the separate release permission in addition to review access.
            // Canencia: The service creates the handover record and marks the item Borrowed together.
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "The release notes are too long.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return Finish(_reservationService.ReleaseEquipment(UserId, id, model?.Notes), id);
        }

        private IActionResult Finish(ReservationResult result, long id)
        {
            // Canencia: Share response handling across the three POST actions: missing record is
            // Canencia: 404; other outcomes become a one-request message on the details redirect.
            if (result.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
