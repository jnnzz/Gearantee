using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using ASI.Basecode.WebApp.Services.EquipmentImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading;

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
        private readonly IEquipmentImageStorage _equipmentImageStorage;

        public CategoriesController(
            ICategoryService categoryService,
            IEquipmentItemService equipmentItemService,
            IEquipmentImageStorage equipmentImageStorage,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _categoryService = categoryService;
            _equipmentItemService = equipmentItemService;
            _equipmentImageStorage = equipmentImageStorage;
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

            ViewData["Title"] = "Equipment & categories";
            ViewData["Eyebrow"] = "Admin workspace";
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
        [RequestSizeLimit(5_500_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 5_500_000)]
        public async Task<IActionResult> CreateEquipment(
            EquipmentItemFormViewModel model,
            [FromForm(Name = "imageFile")] List<IFormFile> imageFiles,
            CancellationToken cancellationToken)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            await PrepareImageUploadAsync(model, imageFiles, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            var promotedImageUrl = await PromoteImageForSaveAsync(model, cancellationToken);
            if (!ModelState.IsValid)
            {
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            EquipmentItemOperationResult result;
            try
            {
                result = await _equipmentItemService.CreateAsync(UserId, model);
            }
            catch
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                throw;
            }

            if (result.Forbidden)
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                return Forbid();
            }

            if (!result.Succeeded)
            {
                await HandleFailedImageSaveAsync(model, result, promotedImageUrl, cancellationToken);

                ModelState.AddModelError(string.Empty, result.Message);
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            model.UploadedImageUrl = null;
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
        [RequestSizeLimit(5_500_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 5_500_000)]
        public async Task<IActionResult> EditEquipment(
            EquipmentItemFormViewModel model,
            [FromForm(Name = "imageFile")] List<IFormFile> imageFiles,
            CancellationToken cancellationToken)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            await PrepareImageUploadAsync(model, imageFiles, cancellationToken);

            if (!ModelState.IsValid)
            {
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            var promotedImageUrl = await PromoteImageForSaveAsync(model, cancellationToken);
            if (!ModelState.IsValid)
            {
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            EquipmentItemOperationResult result;
            try
            {
                result = await _equipmentItemService.UpdateAsync(UserId, model);
            }
            catch
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                throw;
            }

            if (result.Forbidden)
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                return Forbid();
            }

            if (result.Missing)
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                return NotFound();
            }

            if (!result.Succeeded)
            {
                await HandleFailedImageSaveAsync(model, result, promotedImageUrl, cancellationToken);

                ModelState.AddModelError(string.Empty, result.Message);
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            if (model.RemoveImage ||
                (promotedImageUrl != null && !string.Equals(
                    promotedImageUrl, result.PreviousImageUrl, StringComparison.Ordinal)))
            {
                TryDeleteEquipmentImage(result.PreviousImageUrl);
            }

            model.UploadedImageUrl = null;
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
            model.CurrentImageUrl = options.CurrentImageUrl;
            return true;
        }

        private async Task PrepareImageUploadAsync(
            EquipmentItemFormViewModel model,
            IReadOnlyList<IFormFile> imageFiles,
            CancellationToken cancellationToken)
        {
            if (model == null)
            {
                return;
            }

            var previousUploadUrl = model.UploadedImageUrl;
            if (imageFiles?.Count > 1)
            {
                ModelState.AddModelError("imageFile", "Select only one equipment image.");
            }
            else if (imageFiles?.Count == 1 && !string.IsNullOrWhiteSpace(imageFiles[0]?.FileName))
            {
                if (model.RemoveImage)
                {
                    ModelState.AddModelError(nameof(model.RemoveImage), "Choose either a replacement image or remove the current image.");
                }
                else
                {
                    var saved = await _equipmentImageStorage.SaveAsync(imageFiles[0], cancellationToken);
                    if (!saved.Succeeded)
                    {
                        ModelState.AddModelError("imageFile", saved.Error);
                    }
                    else
                    {
                        model.UploadedImageUrl = saved.ImageUrl;
                        if (_equipmentImageStorage.IsStagedImageUrl(previousUploadUrl) &&
                            !string.Equals(previousUploadUrl, saved.ImageUrl, StringComparison.Ordinal))
                        {
                            TryDeleteEquipmentImage(previousUploadUrl);
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(model.UploadedImageUrl) &&
                (!_equipmentImageStorage.IsStagedImageUrl(model.UploadedImageUrl) ||
                 !_equipmentImageStorage.Exists(model.UploadedImageUrl)))
            {
                ModelState.AddModelError(nameof(model.UploadedImageUrl), "Choose the image again; the temporary upload is no longer available.");
                model.UploadedImageUrl = null;
            }

            if (model.RemoveImage && (imageFiles == null || imageFiles.Count == 0))
            {
                TryDeleteEquipmentImage(model.UploadedImageUrl);
                model.UploadedImageUrl = null;
            }

        }

        private async Task<string> PromoteImageForSaveAsync(
            EquipmentItemFormViewModel model,
            CancellationToken cancellationToken)
        {
            if (model.RemoveImage)
            {
                // Explicit removal takes precedence over a URL entered in the secondary field.
                model.ImageUrl = null;
                return null;
            }

            // With no staged upload, preserve the optional URL input. When a file was
            // selected, the promoted managed URL takes precedence over that input.
            if (string.IsNullOrWhiteSpace(model.UploadedImageUrl)) return null;

            var imageUrl = await _equipmentImageStorage.PromoteAsync(
                model.UploadedImageUrl,
                cancellationToken);
            if (imageUrl == null)
            {
                ModelState.AddModelError(
                    nameof(model.UploadedImageUrl),
                    "Choose the image again; the temporary upload is no longer available.");
                model.UploadedImageUrl = null;
                return null;
            }

            model.ImageUrl = imageUrl;
            return imageUrl;
        }

        private async Task HandleFailedImageSaveAsync(
            EquipmentItemFormViewModel model,
            EquipmentItemOperationResult result,
            string promotedImageUrl,
            CancellationToken cancellationToken)
        {
            if (promotedImageUrl == null)
            {
                return;
            }

            if (result.PersistenceFailure)
            {
                TryDeleteEquipmentImage(promotedImageUrl);
                model.UploadedImageUrl = null;
                model.ImageUrl = null;
                return;
            }

            model.UploadedImageUrl = await _equipmentImageStorage.RevertToStagedAsync(
                promotedImageUrl,
                cancellationToken);
            model.ImageUrl = null;
            if (model.UploadedImageUrl == null)
            {
                ModelState.AddModelError(
                    nameof(model.UploadedImageUrl),
                    "The uploaded image could not be retained. Please choose it again.");
            }
        }

        private void TryDeleteEquipmentImage(string imageUrl)
        {
            try
            {
                _equipmentImageStorage.Delete(imageUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Could not clean up managed equipment image {ImageUrl}", imageUrl);
            }
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
