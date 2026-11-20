using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Catalog;
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
    /// Equipment catalog: search and view reservable items (WBS 34.00). Any role holding
    /// equipment.browse can read it; submitting a reservation stays Borrower-only in
    /// ReservationController.
    /// </summary>
    [Authorize(Policy = DomainValues.Permissions.EquipmentBrowse)]
    public class CatalogController : ControllerBase<CatalogController>
    {
        private readonly ICatalogService _catalogService;

        /// <summary>
        /// Constructor
        /// </summary>
        public CatalogController(IHttpContextAccessor httpContextAccessor,
                                 ILoggerFactory loggerFactory,
                                 IConfiguration configuration,
                                 ICatalogService catalogService) : base(httpContextAccessor, loggerFactory, configuration)
        {
            _catalogService = catalogService;
        }

        // GET: /Catalog
        [HttpGet]
        public IActionResult Index([FromQuery] CatalogFilterViewModel filter)
        {
            // Cataraja: WBS 34 flow: Catalog/Index.cshtml -> this action -> CatalogService.
            // Cataraja: Strip legacy date query values here; the current list uses search/category.
            if (Request.Query.ContainsKey("From") || Request.Query.ContainsKey("To"))
            {
                return RedirectToAction(nameof(Index), new { filter.Search, filter.CategoryId });
            }

            var model = _catalogService.GetCatalog(filter);
            SetPageData("Equipment catalog");
            return View(model);
        }

        // GET: /Catalog/Details/5?from=...&to=...
        [HttpGet]
        public IActionResult Details(long id, DateTime? from, DateTime? to)
        {
            // Cataraja: WBS 34: load one reservable item and its booked windows for
            // Cataraja: Details.cshtml; a reservation link continues to ReservationController.Create.
            var item = _catalogService.RetrieveCatalogItem(id);
            if (item == null) return NotFound();

            SetPageData(item.ItemName);
            ViewData["From"] = from;
            ViewData["To"] = to;
            return View(item);
        }

        private void SetPageData(string title)
        {
            // Cataraja: WBS 58 (shared UI): send one title and the caller's workspace
            // Cataraja: label to the shared layout rather than duplicating page headings in each view.
            ViewData["Title"] = title;
            ViewData["Eyebrow"] = User.IsInRole(DomainValues.Roles.Administrator) ? "Administrator workspace"
                : User.IsInRole(DomainValues.Roles.Custodian) ? "Custodian workspace"
                : "Borrower workspace";
        }
    }
}
