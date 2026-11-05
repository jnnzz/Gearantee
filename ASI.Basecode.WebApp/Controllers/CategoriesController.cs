using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Configuration;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using ASI.Basecode.Services.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
        // Allow the whole request to be read so an oversize photo is rejected with a
        // friendly validation message instead of aborting the connection mid-upload.
        // The actual file cap (Supabase:MaxFileBytes) is enforced by validation.
        private const long MaxImageRequestBytes = 25 * 1024 * 1024;
        private const int MaxImageBufferThreshold = 1 * 1024 * 1024;

        private readonly ICategoryService _categoryService;
        private readonly IEquipmentItemService _equipmentItemService;
        private readonly IImageStorageService _imageStorage;
        private readonly SupabaseOptions _supabaseOptions;

        public CategoriesController(
            ICategoryService categoryService,
            IEquipmentItemService equipmentItemService,
            IImageStorageService imageStorage,
            IOptions<SupabaseOptions> supabaseOptions,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(httpContextAccessor, loggerFactory, configuration)
        {
            _categoryService = categoryService;
            _equipmentItemService = equipmentItemService;
            _imageStorage = imageStorage;
            _supabaseOptions = supabaseOptions.Value;
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
            PrepareImageUploadViewData();
            return View("CreateEquipment", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImageRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxImageRequestBytes,
            MemoryBufferThreshold = MaxImageBufferThreshold)]
        public async Task<IActionResult> CreateEquipment(
            EquipmentItemFormViewModel model,
            IFormFile imageFile)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            PrepareImageUploadViewData();

            if (!ModelState.IsValid)
            {
                await PrepareEquipmentFormAsync(model, isEdit: false);
                return View("CreateEquipment", model);
            }

            string uploadedUrl = null;
            if (HasFile(imageFile))
            {
                uploadedUrl = await TryUploadImageAsync(imageFile, ModelState);
                if (uploadedUrl == null)
                {
                    await PrepareEquipmentFormAsync(model, isEdit: false);
                    return View("CreateEquipment", model);
                }

                model.ImageUrl = uploadedUrl;
            }

            var result = await _equipmentItemService.CreateAsync(UserId, model);
            if (result.Forbidden)
            {
                await DeleteImageAsync(uploadedUrl);
                return Forbid();
            }

            if (!result.Succeeded)
            {
                // The database rejected the insert, so the just-uploaded object is orphaned.
                await DeleteImageAsync(uploadedUrl);
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
            PrepareImageUploadViewData();
            return View("EditEquipment", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImageRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxImageRequestBytes,
            MemoryBufferThreshold = MaxImageBufferThreshold)]
        public async Task<IActionResult> EditEquipment(
            EquipmentItemFormViewModel model,
            IFormFile imageFile,
            bool removeImage)
        {
            if (!await _categoryService.CanManageAsync(UserId))
            {
                return Forbid();
            }

            PrepareImageUploadViewData();

            if (!ModelState.IsValid)
            {
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            // The stored URL is authoritative; the posted hidden value is never trusted for
            // deciding which old object to delete.
            var currentImageUrl = await _equipmentItemService.GetImageUrlAsync(
                UserId, model.EquipmentId);

            string uploadedUrl = null;
            if (HasFile(imageFile))
            {
                uploadedUrl = await TryUploadImageAsync(imageFile, ModelState);
                if (uploadedUrl == null)
                {
                    if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                    {
                        return NotFound();
                    }

                    return View("EditEquipment", model);
                }

                model.ImageUrl = uploadedUrl;
            }
            else if (removeImage)
            {
                model.ImageUrl = null;
            }
            else if (_supabaseOptions.IsConfigured)
            {
                // Upload is the only way to change the photo once storage is enabled, so a
                // posted URL cannot silently replace the stored one.
                model.ImageUrl = currentImageUrl;
            }

            var result = await _equipmentItemService.UpdateAsync(UserId, model);
            if (result.Forbidden)
            {
                await DeleteImageAsync(uploadedUrl);
                return Forbid();
            }

            if (result.Missing)
            {
                await DeleteImageAsync(uploadedUrl);
                return NotFound();
            }

            if (!result.Succeeded)
            {
                // The update did not commit, so remove the new object instead of leaving it
                // unreferenced in the bucket.
                await DeleteImageAsync(uploadedUrl);
                ModelState.AddModelError(string.Empty, result.Message);
                if (!await PrepareEquipmentFormAsync(model, isEdit: true))
                {
                    return NotFound();
                }

                return View("EditEquipment", model);
            }

            // Published references can be shared or reused by concurrent edits; keep their objects.
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
            PrepareImageUploadViewData();

            var options = isEdit
                ? await _equipmentItemService.GetEditFormAsync(UserId, model.EquipmentId)
                : await _equipmentItemService.GetCreateFormAsync(UserId);
            if (options == null)
            {
                model.Categories = new List<EquipmentCategoryOptionViewModel>();
                return false;
            }

            model.Categories = options.Categories ?? new List<EquipmentCategoryOptionViewModel>();
            model.IsLocked = options.IsLocked;
            model.IsCategoryInactive = options.IsCategoryInactive;
            return true;
        }

        /// <summary>
        /// The file/drag control always renders; this flag only tells the view whether a
        /// Supabase project is configured so uploads can actually be accepted.
        /// </summary>
        private void PrepareImageUploadViewData()
        {
            ViewData["ImageUploadConfigured"] = _supabaseOptions.IsConfigured;
            ViewData["ImageMaxFileBytes"] = _supabaseOptions.MaxFileBytes;
        }

        private static bool HasFile(IFormFile imageFile)
        {
            return imageFile != null && imageFile.Length > 0;
        }

        /// <summary>
        /// Validates and uploads the posted image. Returns the public URL, or null after adding
        /// a validation error to the form. The stream is re-validated on the server because the
        /// browser's declared type is never trusted.
        /// </summary>
        private async Task<string> TryUploadImageAsync(
            IFormFile imageFile,
            ModelStateDictionary modelState)
        {
            if (!_supabaseOptions.IsConfigured)
            {
                modelState.AddModelError("imageFile", "Image upload is not configured.");
                return null;
            }

            try
            {
                await using var stream = imageFile.OpenReadStream();
                var validation = ImageUploadValidation.Validate(
                    stream,
                    imageFile.FileName,
                    imageFile.ContentType,
                    _supabaseOptions.MaxFileBytes);
                if (!validation.IsValid)
                {
                    modelState.AddModelError("imageFile", validation.Error);
                    return null;
                }

                return await _imageStorage.UploadAsync(
                    stream,
                    imageFile.FileName,
                    validation.ContentType);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Equipment image upload failed.");
                modelState.AddModelError(
                    "imageFile",
                    "The image could not be uploaded. Please try again.");
                return null;
            }
        }

        /// <summary>
        /// Best-effort cleanup of an object that is no longer referenced. A storage failure must
        /// never fail the user's request, so it is only logged.
        /// </summary>
        private async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            try
            {
                await _imageStorage.DeleteAsync(imageUrl);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Equipment image cleanup failed.");
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
