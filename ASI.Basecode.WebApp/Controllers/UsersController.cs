using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.UserAdministration;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(
        Roles = DomainValues.Roles.Administrator,
        Policy = DomainValues.Permissions.UserRoleManage)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class UsersController : Controller
    {
        private readonly IUserAdministrationService _userAdministration;

        public UsersController(IUserAdministrationService userAdministration)
        {
            _userAdministration = userAdministration;
        }

        [HttpGet]
        public async Task<IActionResult> Import()
        {
            if (!await _userAdministration.CanManageAsync(UserId)) return Forbid();
            ViewData["Title"] = "Import accounts";
            ViewData["Eyebrow"] = "Administration";
            return View(new ImportUsersViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> ImportTemplate()
        {
            if (!await _userAdministration.CanManageAsync(UserId)) return Forbid();
            return File(System.Text.Encoding.UTF8.GetBytes(UserImportCsv.Header + "\r\n"),
                "text/csv; charset=utf-8", "gearantee-users-template.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UserImportCsv.MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = UserImportCsv.MaxFileBytes,
            MemoryBufferThreshold = UserImportCsv.MaxRequestBytes)]
        public async Task<IActionResult> Import(IFormFile csvFile)
        {
            if (!await _userAdministration.CanManageAsync(UserId)) return Forbid();
            ViewData["Title"] = "Import accounts";
            ViewData["Eyebrow"] = "Administration";
            if (csvFile == null || csvFile.Length == 0 || csvFile.Length > UserImportCsv.MaxFileBytes)
                return View(new ImportUsersViewModel
                {
                    Errors = new[] { new UserImportError(0, "File", "Choose a non-empty UTF-8 CSV file no larger than 1 MiB.") }
                });

            using var stream = csvFile.OpenReadStream();
            var parsed = await UserImportCsv.ParseAsync(stream, HttpContext.RequestAborted);
            if (parsed.Errors.Count > 0)
                return View(new ImportUsersViewModel { Errors = parsed.Errors });
            var result = await _userAdministration.ImportAsync(UserId, parsed.Rows);
            if (result.Forbidden) return Forbid();
            if (result.Errors.Count > 0)
                return View(new ImportUsersViewModel { Errors = result.Errors });

            TempData["SuccessMessage"] = $"Imported {result.CreatedCount} accounts.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string search,
            string status,
            string role,
            int page = 1)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var accounts = await _userAdministration.GetAccountsAsync(
                UserId, search, status, role, page);
            var permissions = await _userAdministration.GetRolePermissionsAsync(UserId);
            if (accounts == null || permissions == null)
            {
                return Forbid();
            }

            ViewData["Title"] = "Users & roles";
            ViewData["Eyebrow"] = "Administration";
            ViewData["PageDate"] = DateTime.Now.ToString("MMM d, yyyy");
            ViewData["PermissionMatrix"] = permissions;
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View(accounts);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            ViewData["Title"] = "Create account";
            ViewData["Eyebrow"] = "Administration";
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Create account";
                ViewData["Eyebrow"] = "Administration";
                return View(model);
            }

            var result = await _userAdministration.CreateAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                ViewData["Title"] = "Create account";
                ViewData["Eyebrow"] = "Administration";
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var model = await _userAdministration.GetUserAsync(UserId, id);
            if (model == null)
            {
                return NotFound();
            }

            ViewData["Title"] = "Edit account";
            ViewData["Eyebrow"] = "Administration";
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var current = await _userAdministration.GetUserAsync(UserId, model.Id);
                if (current == null)
                {
                    return await _userAdministration.CanManageAsync(UserId)
                        ? NotFound() : Forbid();
                }

                model.UserCode = current.UserCode;
                model.IsActive = current.IsActive;
                model.AvailableRoles = current.AvailableRoles;
                model.SelectedRoles ??= new System.Collections.Generic.List<string>();
                ModelState.Remove(nameof(model.UserCode));
                ModelState.Remove(nameof(model.IsActive));
                // Keep the submitted edit stamp so stale changes still conflict.
                ViewData["Title"] = "Edit account";
                ViewData["Eyebrow"] = "Administration";
                return View(model);
            }

            var result = await _userAdministration.UpdateAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (result.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(Edit), new { id = model.Id });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(SetAccountActiveViewModel model)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "The account form is incomplete. Reload and try again.";
                return RedirectToAction(nameof(Edit), new { id = model.UserId });
            }

            var result = await _userAdministration.SetActiveAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (result.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id = model.UserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Permissions(UpdateRolePermissionsViewModel model)
        {
            if (!await _userAdministration.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var result = await _userAdministration.UpdatePermissionsAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private string UserId => User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    }
}
