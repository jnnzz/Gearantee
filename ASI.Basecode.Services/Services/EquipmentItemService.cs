using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class EquipmentItemService : ServiceBase, IEquipmentItemService
    {
        private const int PageSize = PaginationDefaults.PageSize;
        private static readonly string[] EditableStatuses =
        {
            DomainValues.EquipmentStatuses.Available,
            DomainValues.EquipmentStatuses.UnderMaintenance,
            DomainValues.EquipmentStatuses.Unavailable
        };
        private static readonly string[] Conditions =
        {
            DomainValues.ReturnConditions.Good,
            DomainValues.ReturnConditions.Damaged,
            DomainValues.ReturnConditions.NeedsInspection,
            DomainValues.ReturnConditions.UnderRepair
        };

        private readonly AsiBasecodeDBContext _db;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICategoryService _categoryService;
        private readonly IEquipmentItemRepository _equipmentItemRepository;

        public EquipmentItemService(
            IUnitOfWork unitOfWork,
            ICategoryService categoryService,
            IEquipmentItemRepository equipmentItemRepository,
            ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _db = unitOfWork.Context;
            _unitOfWork = unitOfWork;
            _categoryService = categoryService;
            _equipmentItemRepository = equipmentItemRepository;
        }

        public async Task<EquipmentItemIndexViewModel> GetEquipmentItemsAsync(
            string actorUserId,
            string search = null,
            string status = null,
            long? categoryId = null,
            int page = 1)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return null;
            }

            page = Math.Max(1, page);
            search = search?.Trim();
            status = NormalizeStatusFilter(status);

            var query = _db.EquipmentItems
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search;
                query = query.Where(item =>
                    item.ItemCode.Contains(term) ||
                    item.ItemName.Contains(term) ||
                    (item.Brand != null && item.Brand.Contains(term)) ||
                    (item.Model != null && item.Model.Contains(term)) ||
                    (item.SerialNumber != null && item.SerialNumber.Contains(term)) ||
                    item.Location.Contains(term) ||
                    item.Category.CategoryName.Contains(term) ||
                    item.Category.CategoryCode.Contains(term));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(item => item.CategoryId == categoryId.Value);
            }
            else
            {
                categoryId = null;
            }

            if (string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => item.IsArchived);
            }
            else
            {
                query = query.Where(item => !item.IsArchived);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(item => item.ItemStatus == status);
                }
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            page = Math.Min(page, totalPages);
            var now = DateTime.UtcNow;

            var rows = await query
                .OrderBy(item => item.ItemName)
                .ThenBy(item => item.ItemCode)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(item => new EquipmentItemRowViewModel
                {
                    EquipmentId = item.EquipmentId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    CategoryCode = item.Category.CategoryCode,
                    CategoryName = item.Category.CategoryName,
                    Brand = item.Brand,
                    Model = item.Model,
                    SerialNumber = item.SerialNumber,
                    Location = item.Location,
                    ConditionStatus = item.ConditionStatus,
                    ItemStatus = item.ItemStatus,
                    IsArchived = item.IsArchived,
                    IsLocked = item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed ||
                        item.Reservations.Any(reservation =>
                            (reservation.Status == DomainValues.ReservationStatuses.Pending &&
                             reservation.ReservationEnd > now) ||
                            (reservation.Status == DomainValues.ReservationStatuses.Approved &&
                             (reservation.ReleaseRecord == null || reservation.ReleaseRecord.ReturnRecord == null)))
                })
                .ToListAsync();

            return new EquipmentItemIndexViewModel
            {
                Search = search,
                StatusFilter = status,
                CategoryId = categoryId,
                Page = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Items = rows,
                Categories = await LoadCategoryFilterOptionsAsync()
            };
        }

        public async Task<EquipmentItemFormViewModel> GetCreateFormAsync(string actorUserId)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return null;
            }

            return new EquipmentItemFormViewModel
            {
                Categories = await LoadCategoryOptionsAsync()
            };
        }

        public async Task<EquipmentItemFormViewModel> GetEditFormAsync(
            string actorUserId,
            long equipmentId)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return null;
            }

            var item = await _db.EquipmentItems
                .AsNoTracking()
                .Include(equipment => equipment.Category)
                .SingleOrDefaultAsync(equipment =>
                    equipment.EquipmentId == equipmentId && !equipment.IsArchived);
            if (item == null)
            {
                return null;
            }

            var isLocked = await IsLockedAsync(item);
            return new EquipmentItemFormViewModel
            {
                EquipmentId = item.EquipmentId,
                CategoryId = item.CategoryId,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                Brand = item.Brand,
                ModelName = item.Model,
                SerialNumber = item.SerialNumber,
                Location = item.Location,
                ConditionStatus = item.ConditionStatus,
                ItemStatus = item.ItemStatus,
                IsLocked = isLocked,
                IsCategoryInactive = !item.Category.IsActive,
                Categories = await LoadCategoryOptionsAsync(item.CategoryId)
            };
        }

        public async Task<EquipmentItemOperationResult> CreateAsync(
            string actorUserId,
            EquipmentItemFormViewModel model)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return EquipmentItemOperationResult.AccessDenied();
            }

            if (model == null)
            {
                return EquipmentItemOperationResult.Failure("Equipment details are required.");
            }

            var normalized = Normalize(model);
            if (!normalized.IsValid)
            {
                return EquipmentItemOperationResult.Failure(normalized.Error);
            }

            var category = await _db.EquipmentCategories
                .SingleOrDefaultAsync(candidate => candidate.CategoryId == model.CategoryId);
            if (category == null || !category.IsActive)
            {
                return EquipmentItemOperationResult.Failure(
                    "Choose an active equipment category.");
            }

            if (!await IsItemCodeAvailableAsync(normalized.ItemCode, null))
            {
                return EquipmentItemOperationResult.Failure(
                    $"An equipment item with code '{normalized.ItemCode}' already exists.");
            }

            if (normalized.SerialNumber != null &&
                !await IsSerialNumberAvailableAsync(normalized.SerialNumber, null))
            {
                return EquipmentItemOperationResult.Failure(
                    $"An equipment item with serial number '{normalized.SerialNumber}' already exists.");
            }

            var item = new EquipmentItem
            {
                CategoryId = category.CategoryId,
                ItemCode = normalized.ItemCode,
                ItemName = normalized.ItemName,
                Description = normalized.Description,
                ImageUrl = normalized.ImageUrl,
                Brand = normalized.Brand,
                Model = normalized.ModelName,
                SerialNumber = normalized.SerialNumber,
                Location = normalized.Location,
                ConditionStatus = normalized.ConditionStatus,
                ItemStatus = normalized.ItemStatus,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _db.EquipmentItems.Add(item);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation(
                    "Equipment item {EquipmentId} created by administrator {UserId}",
                    item.EquipmentId,
                    actorUserId);
                return EquipmentItemOperationResult.Success(
                    $"Equipment item '{item.ItemName}' was registered.",
                    item.EquipmentId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Database error registering equipment item.");
                return EquipmentItemOperationResult.Failure(
                    "An item with this code or serial number already exists.");
            }
        }

        public async Task<EquipmentItemOperationResult> UpdateAsync(
            string actorUserId,
            EquipmentItemFormViewModel model)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return EquipmentItemOperationResult.AccessDenied();
            }

            if (model == null)
            {
                return EquipmentItemOperationResult.Failure("Equipment details are required.");
            }

            // Inventory and reservation workflows must acquire the equipment row lock
            // before checking reservations or changing state. Reservation creation and
            // approval must use EquipmentItemConcurrencyLock in their own Serializable
            // transaction before validating and writing the reservation.
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
            // The execution strategy re-runs this delegate after a transient fault using the
            // same DbContext. Drop any tracked state left by a failed attempt so the retry
            // reads the committed database values instead of reusing uncommitted changes.
            _db.ChangeTracker.Clear();

            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var item = await _equipmentItemRepository.LoadForUpdateAsync(
                model.EquipmentId);
            if (item == null)
            {
                return EquipmentItemOperationResult.NotFound();
            }

            if (await IsLockedAsync(item))
            {
                return EquipmentItemOperationResult.Failure(
                    "This item has a pending reservation or active loan and cannot be changed until that transaction is closed.");
            }

            var normalized = Normalize(model);
            if (!normalized.IsValid)
            {
                return EquipmentItemOperationResult.Failure(normalized.Error);
            }

            var category = await _db.EquipmentCategories
                .SingleOrDefaultAsync(candidate => candidate.CategoryId == model.CategoryId);
            if (category == null || (!category.IsActive && category.CategoryId != item.CategoryId))
            {
                return EquipmentItemOperationResult.Failure(
                    "Choose an active equipment category.");
            }

            if (!await IsItemCodeAvailableAsync(normalized.ItemCode, item.EquipmentId))
            {
                return EquipmentItemOperationResult.Failure(
                    $"Another equipment item with code '{normalized.ItemCode}' already exists.");
            }

            if (normalized.SerialNumber != null &&
                !await IsSerialNumberAvailableAsync(normalized.SerialNumber, item.EquipmentId))
            {
                return EquipmentItemOperationResult.Failure(
                    $"Another equipment item with serial number '{normalized.SerialNumber}' already exists.");
            }

            item.CategoryId = category.CategoryId;
            item.ItemCode = normalized.ItemCode;
            item.ItemName = normalized.ItemName;
            item.Description = normalized.Description;
            item.ImageUrl = normalized.ImageUrl;
            item.Brand = normalized.Brand;
            item.Model = normalized.ModelName;
            item.SerialNumber = normalized.SerialNumber;
            item.Location = normalized.Location;
            item.ConditionStatus = normalized.ConditionStatus;
            item.ItemStatus = normalized.ItemStatus;
            item.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation(
                    "Equipment item {EquipmentId} updated by administrator {UserId}",
                    item.EquipmentId,
                    actorUserId);
                return EquipmentItemOperationResult.Success(
                    $"Equipment item '{item.ItemName}' was updated.",
                    item.EquipmentId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception,
                    "Database error updating equipment item {EquipmentId}", item.EquipmentId);
                return EquipmentItemOperationResult.Failure(
                    "An item with this code or serial number already exists.");
            }
            });
        }

        public async Task<EquipmentItemOperationResult> SetArchivedAsync(
            string actorUserId,
            long equipmentId,
            bool isArchived)
        {
            if (!await _categoryService.CanManageAsync(actorUserId))
            {
                return EquipmentItemOperationResult.AccessDenied();
            }

            // Archive must use the same serializable lock/check/write boundary as Edit.
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
            // The execution strategy re-runs this delegate after a transient fault using the
            // same DbContext. Reload from committed state on every attempt: otherwise an
            // attempt that saved IsArchived=true but failed to commit leaves the value tracked,
            // and the retry reports "already archived" without writing anything. Re-reading
            // also resolves an uncertain commit correctly, because the retry inspects the
            // actual stored value before deciding whether a write is needed.
            _db.ChangeTracker.Clear();

            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var item = await _equipmentItemRepository.LoadForUpdateAsync(
                equipmentId, includeArchived: true);
            if (item == null)
            {
                return EquipmentItemOperationResult.NotFound();
            }

            await _db.Entry(item).Reference(equipment => equipment.Category).LoadAsync();

            if (item.IsArchived == isArchived)
            {
                return EquipmentItemOperationResult.Success(
                    $"Equipment item is already {(isArchived ? "archived" : "active inventory") }.",
                    item.EquipmentId);
            }

            if (isArchived && await IsLockedAsync(item))
            {
                return EquipmentItemOperationResult.Failure(
                    "This item has a pending reservation or active loan and cannot be archived until that transaction is closed.");
            }

            if (!isArchived && !item.Category.IsActive)
            {
                return EquipmentItemOperationResult.Failure(
                    "Activate the item's category before restoring this equipment item.");
            }

            item.IsArchived = isArchived;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();

            var action = isArchived ? "archived" : "restored to active inventory";
            _logger.LogInformation(
                "Equipment item {EquipmentId} {Action} by administrator {UserId}",
                item.EquipmentId,
                action,
                actorUserId);
            return EquipmentItemOperationResult.Success(
                $"Equipment item '{item.ItemName}' was {action}.",
                item.EquipmentId);
            });
        }

        private async Task<IReadOnlyList<EquipmentCategoryOptionViewModel>> LoadCategoryOptionsAsync(
            long? includeCategoryId = null)
        {
            return await _db.EquipmentCategories
                .AsNoTracking()
                .Where(category => category.IsActive ||
                    (includeCategoryId.HasValue && category.CategoryId == includeCategoryId.Value))
                .OrderBy(category => category.CategoryName)
                .Select(category => new EquipmentCategoryOptionViewModel
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    IsActive = category.IsActive
                })
                .ToListAsync();
        }

        private async Task<IReadOnlyList<EquipmentCategoryOptionViewModel>> LoadCategoryFilterOptionsAsync()
        {
            return await _db.EquipmentCategories
                .AsNoTracking()
                .OrderBy(category => category.CategoryName)
                .Select(category => new EquipmentCategoryOptionViewModel
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    IsActive = category.IsActive
                })
                .ToListAsync();
        }

        private async Task<bool> IsItemCodeAvailableAsync(string itemCode, long? excludingId)
        {
            var normalized = itemCode.ToLowerInvariant();
            return !await _db.EquipmentItems.AsNoTracking().AnyAsync(item =>
                (!excludingId.HasValue || item.EquipmentId != excludingId.Value) &&
                item.ItemCode.ToLower() == normalized);
        }

        private async Task<bool> IsSerialNumberAvailableAsync(string serialNumber, long? excludingId)
        {
            var normalized = serialNumber.ToLowerInvariant();
            return !await _db.EquipmentItems.AsNoTracking().AnyAsync(item =>
                (!excludingId.HasValue || item.EquipmentId != excludingId.Value) &&
                item.SerialNumber != null && item.SerialNumber.ToLower() == normalized);
        }

        private async Task<bool> IsLockedAsync(EquipmentItem item)
        {
            if (item.ItemStatus == DomainValues.EquipmentStatuses.Borrowed)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            return await _db.Reservations.AsNoTracking().AnyAsync(reservation =>
                reservation.EquipmentId == item.EquipmentId &&
                ((reservation.Status == DomainValues.ReservationStatuses.Pending &&
                  reservation.ReservationEnd > now) ||
                 (reservation.Status == DomainValues.ReservationStatuses.Approved &&
                  (reservation.ReleaseRecord == null || reservation.ReleaseRecord.ReturnRecord == null))));
        }

        private static string NormalizeStatusFilter(string status)
        {
            if (string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase))
            {
                return "archived";
            }

            return EditableStatuses.Concat(new[] { DomainValues.EquipmentStatuses.Borrowed })
                .FirstOrDefault(value => string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static NormalizedEquipmentItem Normalize(EquipmentItemFormViewModel model)
        {
            var itemCode = model.ItemCode?.Trim().ToUpperInvariant();
            var itemName = model.ItemName?.Trim();
            var location = model.Location?.Trim();
            var condition = Conditions.FirstOrDefault(value =>
                string.Equals(value, model.ConditionStatus?.Trim(), StringComparison.OrdinalIgnoreCase));
            var status = EditableStatuses.FirstOrDefault(value =>
                string.Equals(value, model.ItemStatus?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(itemCode) || string.IsNullOrWhiteSpace(itemName) ||
                string.IsNullOrWhiteSpace(location))
            {
                return NormalizedEquipmentItem.Invalid("Item code, item name, and storage location are required.");
            }

            if (itemCode.Length > 100 || itemName.Length > 200 || location.Length > 200)
            {
                return NormalizedEquipmentItem.Invalid("One or more equipment fields exceed the allowed length.");
            }

            if (condition == null)
            {
                return NormalizedEquipmentItem.Invalid("Choose a valid equipment condition.");
            }

            if (status == null)
            {
                return NormalizedEquipmentItem.Invalid("Choose a valid equipment status.");
            }

            if (status == DomainValues.EquipmentStatuses.Available &&
                condition != DomainValues.ReturnConditions.Good)
            {
                return NormalizedEquipmentItem.Invalid(
                    "Only equipment in Good condition can be marked Available.");
            }

            var serialNumber = NullIfWhiteSpace(model.SerialNumber);
            var imageUrl = NullIfWhiteSpace(model.ImageUrl);
            var brand = NullIfWhiteSpace(model.Brand);
            var modelName = NullIfWhiteSpace(model.ModelName);
            var description = NullIfWhiteSpace(model.Description);
            if ((brand?.Length ?? 0) > 150 || (modelName?.Length ?? 0) > 150 ||
                (serialNumber?.Length ?? 0) > 200 || (description?.Length ?? 0) > 4000)
            {
                return NormalizedEquipmentItem.Invalid("One or more equipment fields exceed the allowed length.");
            }

            if ((imageUrl?.Length ?? 0) > 500 || !IsSupportedImageUrl(imageUrl))
            {
                return NormalizedEquipmentItem.Invalid(
                    "Catalog image must be an HTTPS URL or a same-site path beginning with '/'.");
            }

            return new NormalizedEquipmentItem
            {
                IsValid = true,
                ItemCode = itemCode,
                ItemName = itemName,
                Description = description,
                ImageUrl = imageUrl,
                Brand = brand,
                ModelName = modelName,
                SerialNumber = serialNumber,
                Location = location,
                ConditionStatus = condition,
                ItemStatus = status
            };
        }

        private static string NullIfWhiteSpace(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool IsSupportedImageUrl(string imageUrl)
        {
            if (imageUrl == null)
            {
                return true;
            }

            if (imageUrl.StartsWith("/", StringComparison.Ordinal))
            {
                return !imageUrl.StartsWith("//", StringComparison.Ordinal) &&
                    !imageUrl.Contains((char)92) &&
                    !imageUrl.Any(char.IsControl);
            }

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri))
            {
                return string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(absoluteUri.Host) &&
                    string.IsNullOrEmpty(absoluteUri.UserInfo);
            }

            return false;
        }

        private sealed class NormalizedEquipmentItem
        {
            public bool IsValid { get; set; }
            public string Error { get; private set; }
            public string ItemCode { get; set; }
            public string ItemName { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
            public string Brand { get; set; }
            public string ModelName { get; set; }
            public string SerialNumber { get; set; }
            public string Location { get; set; }
            public string ConditionStatus { get; set; }
            public string ItemStatus { get; set; }

            public static NormalizedEquipmentItem Invalid(string error) =>
                new() { Error = error };
        }
    }
}
