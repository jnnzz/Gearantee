using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Reservations;
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
    /// <summary>
    /// Borrower reservations: submit a request (WBS 35.00), view status (WBS 36.00), and cancel (WBS 37.00).
    /// Borrowers can always see and cancel their own reservations; only submitting a new
    /// request needs reservation.create.
    /// </summary>
    [Authorize(Roles = DomainValues.Roles.Borrower)]
    public class ReservationController : ControllerBase<ReservationController>
    {
        private readonly IReservationService _reservationService;
        private readonly ICatalogService _catalogService;

        /// <summary>
        /// Constructor
        /// </summary>
        public ReservationController(IHttpContextAccessor httpContextAccessor,
                                     ILoggerFactory loggerFactory,
                                     IConfiguration configuration,
                                     IReservationService reservationService,
                                     ICatalogService catalogService) : base(httpContextAccessor, loggerFactory, configuration)
        {
            _reservationService = reservationService;
            _catalogService = catalogService;
        }

        // GET: /Reservation?status=Pending&page=1
        [HttpGet]
        public IActionResult Index(string status, int page = 1)
        {
            var model = _reservationService.GetMyReservations(UserId, status, page);
            SetPageData("My reservations");
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View(model);
        }

        // GET: /Reservation/Details/5
        [HttpGet]
        public IActionResult Details(long id)
        {
            var reservation = _reservationService.RetrieveMyReservation(id, UserId);
            if (reservation == null) return NotFound();

            SetPageData(reservation.ReservationCode);
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View(reservation);
        }

        // GET: /Reservation/Create?equipmentId=5&from=...&to=...
        [HttpGet]
        [Authorize(Policy = DomainValues.Permissions.ReservationCreate)]
        public IActionResult Create(long equipmentId, DateTime? from, DateTime? to)
        {
            var item = _catalogService.RetrieveCatalogItem(equipmentId);
            if (item == null) return NotFound();

            var model = new ReservationCreateViewModel
            {
                EquipmentId = equipmentId,
                ReservationStart = from,
                ReservationEnd = to,
                Item = item
            };

            SetCreatePageData();
            return View(model);
        }

        // POST: /Reservation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = DomainValues.Permissions.ReservationCreate)]
        public IActionResult Create(ReservationCreateViewModel model)
        {
            model.Item = _catalogService.RetrieveCatalogItem(model.EquipmentId);
            if (model.Item == null) return NotFound();

            if (!ModelState.IsValid)
            {
                SetCreatePageData();
                return View(model);
            }

            var result = _reservationService.SubmitReservation(model, UserId);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                SetCreatePageData();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = result.ReservationId });
        }

        // POST: /Reservation/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(long id)
        {
            var result = _reservationService.CancelReservation(id, UserId);
            if (result.NotFound) return NotFound();

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private void SetCreatePageData()
        {
            SetPageData("Request a reservation");
            ViewData["EligibilityError"] = _reservationService.CheckBorrowerEligibility(UserId);
        }

        private void SetPageData(string title)
        {
            ViewData["Title"] = title;
            ViewData["Eyebrow"] = "Borrower workspace";
        }
    }
}
