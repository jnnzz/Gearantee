using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Dashboard;
using ASI.Basecode.Services.Utilities;
using ASI.Basecode.WebApp.Models.Dashboard;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class DashboardController : ControllerBase<DashboardController>
    {
        private readonly IDashboardService _dashboardService;
        private readonly IHostEnvironment _environment;

        public DashboardController(
            IDashboardService dashboardService,
            IHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _dashboardService = dashboardService;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole(DomainValues.Roles.Administrator))
            {
                return RedirectToAction(nameof(Administrator));
            }

            if (User.IsInRole(DomainValues.Roles.Custodian))
            {
                return RedirectToAction(nameof(Custodian));
            }

            if (User.IsInRole(DomainValues.Roles.Borrower))
            {
                return RedirectToAction(nameof(Borrower));
            }

            return Forbid();
        }

        [HttpGet]
        [Authorize(Roles = DomainValues.Roles.Borrower)]
        public async Task<IActionResult> Borrower()
        {
            var localNow = ManilaClock.NowLocal;
            var model = new BorrowerDashboardModel
            {
                DisplayName = User.FindFirst("display_name")?.Value ?? "there"
            };

            try
            {
                model = await _dashboardService.GetBorrowerDashboardAsync(UserId);
            }
            catch (Exception exception)
            {
                HandleExceptionLog(exception, "Load borrower dashboard");
                ViewData["DashboardError"] =
                    "Dashboard information could not be loaded. Refresh the page or contact support.";
            }

            var localHour = localNow.Hour;
            var greeting = localHour < 12
                ? "morning"
                : localHour < 18 ? "afternoon" : "evening";
            ViewData["Title"] = $"Good {greeting}, {model.DisplayName}";
            ViewData["Eyebrow"] = "Borrower workspace";
            ViewData["Workspace"] = "Borrower workspace";
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = DomainValues.Roles.Custodian)]
        public async Task<IActionResult> Custodian()
        {
            var localNow = ManilaClock.NowLocal;
            var model = new CustodianDashboardModel();
            try
            {
                model = await _dashboardService.GetCustodianDashboardAsync();
            }
            catch (Exception exception)
            {
                HandleExceptionLog(exception, "Load custodian dashboard");
                ViewData["DashboardError"] =
                    "Dashboard information could not be loaded. Refresh the page or contact support.";
            }

            ViewData["Title"] = "Equipment desk";
            ViewData["Eyebrow"] = "Custodian workspace";
            ViewData["Workspace"] = "Custodian workspace";
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = DomainValues.Roles.Administrator)]
        public async Task<IActionResult> Administrator()
        {
            var localNow = ManilaClock.NowLocal;
            var model = new AdministratorDashboardModel();
            try
            {
                model = await _dashboardService.GetAdministratorDashboardAsync();
            }
            catch (Exception exception)
            {
                HandleExceptionLog(exception, "Load administrator dashboard");
                ViewData["DashboardError"] =
                    "Dashboard information could not be loaded. Refresh the page or contact support.";
            }

            ViewData["Title"] = "System overview";
            ViewData["Eyebrow"] = "Administrator workspace";
            ViewData["Workspace"] = "Administrator workspace";
            return View(model);
        }
    }
}
