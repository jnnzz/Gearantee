using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.AvailabilityCalendar;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = DomainValues.Permissions.EquipmentBrowse)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AvailabilityCalendarController : ControllerBase<AvailabilityCalendarController>
    {
        private const double MaxRangeDays = 8;
        private readonly IAvailabilityCalendarService _availabilityCalendarService;

        public AvailabilityCalendarController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IAvailabilityCalendarService availabilityCalendarService)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _availabilityCalendarService = availabilityCalendarService;
        }

        [HttpGet]
        public IActionResult Index([FromQuery] AvailabilityCalendarFilterViewModel filter)
        {
            filter ??= new AvailabilityCalendarFilterViewModel();
            if (!string.IsNullOrEmpty(filter.Date) &&
                !DateOnly.TryParseExact(filter.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
            {
                ModelState.AddModelError(nameof(filter.Date), "The calendar date is invalid.");
            }

            if (!ModelState.IsValid)
            {
                filter.CategoryId = filter.CategoryId > 0 ? filter.CategoryId : null;
                filter.EquipmentId = filter.EquipmentId > 0 ? filter.EquipmentId : null;
                if (filter.Location?.Length > 200)
                {
                    filter.Location = null;
                }
                filter.Date = null;
                filter.View = null;
            }

            var model = _availabilityCalendarService.GetCalendarPage(filter);
            model.CanCreateReservations = User.IsInRole(DomainValues.Roles.Borrower) &&
                User.HasClaim("permission", DomainValues.Permissions.ReservationCreate);
            if (filter.CategoryId.HasValue &&
                !model.Categories.Exists(category => category.CategoryId == filter.CategoryId.Value))
            {
                model.ValidationMessage = "That equipment category is not available.";
            }
            else if (!ModelState.IsValid)
            {
                model.ValidationMessage = "One or more calendar filters were invalid and have been cleared.";
            }

            SetPageData();
            return View(model);
        }

        [HttpGet]
        public IActionResult Events(
            [FromQuery] AvailabilityCalendarFilterViewModel filter,
            DateTimeOffset? start,
            DateTimeOffset? end)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "One or more calendar filters are invalid." });
            }

            if (!start.HasValue || !end.HasValue ||
                end.Value <= start.Value ||
                (end.Value - start.Value).TotalDays > MaxRangeDays)
            {
                return BadRequest(new { message = "Choose a valid calendar period of eight days or less." });
            }

            var result = _availabilityCalendarService.GetCalendarData(filter, start.Value, end.Value);
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity,
                    new { message = result.ErrorMessage });
            }

            return Json(result);
        }

        private void SetPageData()
        {
            ViewData["Title"] = "Availability calendar";
            ViewData["Eyebrow"] = "Reports";
        }
    }
}
