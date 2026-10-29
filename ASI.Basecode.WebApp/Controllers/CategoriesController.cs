using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(
        Roles = DomainValues.Roles.Administrator,
        Policy = DomainValues.Permissions.EquipmentManage)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class CategoriesController : Mvc.ControllerBase<CategoriesController>
    {
        private readonly ICategoryService _categoryService;
        private readonly IEquipmentItemService _equipmentItemService;

        public CategoriesController(
            ICategoryService categoryService,
            IEquipmentItemService equipmentItemService,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _categoryService = categoryService;
            _equipmentItemService = equipmentItemService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string tab = "equipment",
            string categorySearch = null,
            string categoryStatus = null,
            int categoryPage = 1,
            string equipmentSearch = null,
            string equipmentStatus = null,
            long? equipmentCategoryId = null,
            int equipmentPage = 1)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var model = await _categoryService.GetEquipmentManagementAsync(
                UserId,
                tab,
                categorySearch,
                categoryStatus,
                categoryPage);

            if (model == null)
            {
                return Forbid();
            }

            model.Equipment = await _equipmentItemService.GetEquipmentItemsAsync(
                UserId,
                equipmentSearch,
                equipmentStatus,
                equipmentCategoryId,
                equipmentPage);
            if (model.Equipment == null)
            {
                return Forbid();
            }

            ViewData["Title"] = "Equipments & categories";
            ViewData["Eyebrow"] = "Master data";
            ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateEquipment()
        {
            var model = await _equipmentItemService.GetCreateFormAsync(UserId);
            if (model == null)
            {
                return Forbid();
            }

            ViewData["Title"] = "Register equipment item";
            ViewData["Eyebrow"] = "Master data";
            return View("CreateEquipment", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEquipment(EquipmentItemFormViewModel model)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            var result = await _equipmentItemService.CreateAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "equipment" });
        }

        [HttpGet]
        public async Task<IActionResult> EditEquipment(long id)
        {
            var model = await _equipmentItemService.GetEditFormAsync(UserId, id);
            if (model == null)
            {
                if (!await _categoryService.CanManageAsync(UserId))
                {
                    return Forbid();
                }

                return NotFound();
            }

            ViewData["Title"] = "Edit equipment item";
            ViewData["Eyebrow"] = "Master data";
            return View("EditEquipment", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEquipment(EquipmentItemFormViewModel model)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            var result = await _equipmentItemService.UpdateAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (result.Missing)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "equipment" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetEquipmentArchived(
            long id,
            bool archived,
            string equipmentSearch = null,
            string equipmentStatus = null,
            long? equipmentCategoryId = null,
            int equipmentPage = 1)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var result = await _equipmentItemService.SetArchivedAsync(
                UserId,
                id,
                archived);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (result.Missing)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new
            {
                tab = "equipment",
                equipmentSearch,
                equipmentStatus,
                equipmentCategoryId,
                equipmentPage
            });
        }

        private async Task<bool> PrepareEquipmentFormAsync(
            EquipmentItemFormViewModel model,
            bool isEdit)
        {
            ViewData["Title"] = isEdit ? "Edit equipment item" : "Register equipment item";
            ViewData["Eyebrow"] = "Master data";

            var options = isEdit
                ? await _equipmentItemService.GetEditFormAsync(UserId, model.EquipmentId)
                : await _equipmentItemService.GetCreateFormAsync(UserId);
            if (options == null)
            {
                return false;
            }

            model.Categories = options.Categories;
            model.IsLocked = options.IsLocked;
            model.IsCategoryInactive = options.IsCategoryInactive;
            return true;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            ViewData["Title"] = "Create category";
            ViewData["Eyebrow"] = "Master data";
            return View(new CreateCategoryViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCategoryViewModel model)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (Request.HasFormContentType && Request.Form.TryGetValue(nameof(model.IsActive), out var activeValues))
            {
                model.IsActive = activeValues.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
            }

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Create category";
                ViewData["Eyebrow"] = "Master data";
                return View(model);
            }

            var result = await _categoryService.CreateCategoryAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                ViewData["Title"] = "Create category";
                ViewData["Eyebrow"] = "Master data";
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "categories" });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var category = await _categoryService.GetCategoryByIdAsync(UserId, id);
            if (category == null)
            {
                return NotFound();
            }

            var model = new EditCategoryViewModel
            {
                CategoryId = category.CategoryId,
                CategoryCode = category.CategoryCode,
                CategoryName = category.CategoryName,
                Description = category.Description,
                IsActive = category.IsActive
            };

            ViewData["Title"] = "Edit category";
            ViewData["Eyebrow"] = "Master data";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCategoryViewModel model)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            if (Request.HasFormContentType && Request.Form.TryGetValue(nameof(model.IsActive), out var activeValues))
            {
                model.IsActive = activeValues.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
            }

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Edit category";
                ViewData["Eyebrow"] = "Master data";
                return View(model);
            }

            var result = await _categoryService.UpdateCategoryAsync(UserId, model);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                if (result.Message == "Category not found.")
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Message);
                ViewData["Title"] = "Edit category";
                ViewData["Eyebrow"] = "Master data";
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index), new { tab = "categories" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(SetCategoryActiveModel model)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            var result = await _categoryService.SetCategoryActiveAsync(UserId, model.CategoryId, model.IsActive);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Message;
            }
            else
            {
                TempData["SuccessMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index), new { tab = "categories" });
        }
    }
}
