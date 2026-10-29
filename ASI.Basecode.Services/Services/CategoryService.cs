using ASI.Basecode.Services.Utilities;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Category;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class CategoryService : ServiceBase, ICategoryService
    {
        private const int PageSize = PaginationDefaults.PageSize;
        private readonly AsiBasecodeDBContext _db;
        private readonly IUnitOfWork _unitOfWork;

        public CategoryService(
            IUnitOfWork unitOfWork,
            ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _db = unitOfWork.Context;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> CanManageAsync(string actorUserId)
        {
            if (string.IsNullOrWhiteSpace(actorUserId))
            {
                return false;
            }

            var actor = await _db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == actorUserId);
            if (actor == null || !actor.IsActive)
            {
                return false;
            }

            var hasPermission = await (
                from ur in _db.UserRoles.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                join rp in _db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.PermissionId
                where ur.UserId == actorUserId &&
                      r.Name == DomainValues.Roles.Administrator &&
                      p.PermissionName == DomainValues.Permissions.EquipmentManage
                select p.PermissionId
            ).AnyAsync();

            return hasPermission;
        }

        public async Task<CategoryResult> CreateCategoryAsync(string actorUserId, CreateCategoryViewModel model)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return CategoryResult.AccessDenied();
            }

            if (model == null)
            {
                return CategoryResult.Failure("Category details are required.");
            }

            var code = model.CategoryCode?.Trim().ToUpperInvariant();
            var name = model.CategoryName?.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                return CategoryResult.Failure("Category code is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return CategoryResult.Failure("Category name is required.");
            }

            if (code.Length > 50)
            {
                return CategoryResult.Failure("Category code cannot exceed 50 characters.");
            }

            if (name.Length > 150)
            {
                return CategoryResult.Failure("Category name cannot exceed 150 characters.");
            }

            var codeExists = await _db.EquipmentCategories.AsNoTracking()
                .AnyAsync(category => category.CategoryCode == code);
            if (codeExists)
            {
                return CategoryResult.Failure($"A category with code '{code}' already exists.");
            }

            var nameLower = name.ToLower();
            var nameExists = await _db.EquipmentCategories.AsNoTracking()
                .AnyAsync(category => category.CategoryName.ToLower() == nameLower);
            if (nameExists)
            {
                return CategoryResult.Failure($"A category with name '{name}' already exists.");
            }

            var category = new EquipmentCategory
            {
                CategoryCode = code,
                CategoryName = name,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                _db.EquipmentCategories.Add(category);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Equipment category {Code} ('{Name}') created by user {UserId}", code, name, actorUserId);
                return CategoryResult.Success($"Category '{category.CategoryName}' was created successfully.", category.CategoryId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Database update error creating category {Code}", code);
                return CategoryResult.Failure("A category with this code or name already exists.");
            }
        }

        public async Task<CategoryResult> UpdateCategoryAsync(string actorUserId, EditCategoryViewModel model)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return CategoryResult.AccessDenied();
            }

            if (model == null)
            {
                return CategoryResult.Failure("Category details are required.");
            }

            var category = await _db.EquipmentCategories.FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId);
            if (category == null)
            {
                return CategoryResult.Failure("Category not found.");
            }

            var code = model.CategoryCode?.Trim().ToUpperInvariant();
            var name = model.CategoryName?.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                return CategoryResult.Failure("Category code is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return CategoryResult.Failure("Category name is required.");
            }

            if (code.Length > 50)
            {
                return CategoryResult.Failure("Category code cannot exceed 50 characters.");
            }

            if (name.Length > 150)
            {
                return CategoryResult.Failure("Category name cannot exceed 150 characters.");
            }

            var codeExists = await _db.EquipmentCategories.AsNoTracking()
                .AnyAsync(c => c.CategoryId != model.CategoryId && c.CategoryCode == code);
            if (codeExists)
            {
                return CategoryResult.Failure($"Another category with code '{code}' already exists.");
            }

            var nameLower = name.ToLower();
            var nameExists = await _db.EquipmentCategories.AsNoTracking()
                .AnyAsync(c => c.CategoryId != model.CategoryId && c.CategoryName.ToLower() == nameLower);
            if (nameExists)
            {
                return CategoryResult.Failure($"Another category with name '{name}' already exists.");
            }

            category.CategoryCode = code;
            category.CategoryName = name;
            category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            category.IsActive = model.IsActive;
            category.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Equipment category {CategoryId} updated by user {UserId}", category.CategoryId, actorUserId);
                return CategoryResult.Success($"Category '{category.CategoryName}' updated successfully.", category.CategoryId);
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Database update error updating category {CategoryId}", category.CategoryId);
                return CategoryResult.Failure("A category with this code or name already exists.");
            }
        }

        public async Task<CategoryResult> SetCategoryActiveAsync(string actorUserId, long categoryId, bool isActive)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return CategoryResult.AccessDenied();
            }

            var category = await _db.EquipmentCategories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
            if (category == null)
            {
                return CategoryResult.Failure("Category not found.");
            }

            if (category.IsActive == isActive)
            {
                return CategoryResult.Success($"Category is already {(isActive ? "active" : "inactive")}.", category.CategoryId);
            }

            category.IsActive = isActive;
            category.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Equipment category {CategoryId} activation changed to {IsActive} by user {UserId}", categoryId, isActive, actorUserId);
            var state = isActive ? "activated" : "deactivated";
            return CategoryResult.Success($"Category '{category.CategoryName}' was {state} successfully.", category.CategoryId);
        }

        public async Task<CategoryIndexViewModel> GetCategoriesAsync(
            string actorUserId,
            string search = null,
            string status = null,
            int page = 1)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            if (page < 1) page = 1;

            var query = _db.EquipmentCategories.AsNoTracking().AsQueryable();

            search = search?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(category =>
                    category.CategoryCode.ToLower().Contains(searchLower) ||
                    category.CategoryName.ToLower().Contains(searchLower) ||
                    (category.Description != null && category.Description.ToLower().Contains(searchLower)));
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(category => category.IsActive);
            }
            else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(category => !category.IsActive);
            }
            else
            {
                status = string.Empty;
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            if (page > totalPages) page = totalPages;

            var rows = await query
                .OrderBy(category => category.CategoryName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(category => new CategoryViewModel
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    Description = category.Description,
                    IsActive = category.IsActive,
                    ItemCount = category.EquipmentItems.Count(item => !item.IsArchived),
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt
                })
                .ToListAsync();

            return new CategoryIndexViewModel
            {
                Search = search,
                StatusFilter = status,
                Page = page,
                TotalPages = totalPages,
                TotalCount = totalCount,
                Categories = rows
            };
        }

        public async Task<CategoryViewModel> GetCategoryByIdAsync(string actorUserId, long categoryId)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            var category = await _db.EquipmentCategories.AsNoTracking()
                .Where(c => c.CategoryId == categoryId)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    CategoryCode = c.CategoryCode,
                    CategoryName = c.CategoryName,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    ItemCount = c.EquipmentItems.Count(item => !item.IsArchived),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .SingleOrDefaultAsync();

            return category;
        }

        public async Task<EquipmentManagementIndexViewModel> GetEquipmentManagementAsync(
            string actorUserId,
            string tab = "equipment",
            string categorySearch = null,
            string categoryStatus = null,
            int categoryPage = 1)
        {
            if (!await CanManageAsync(actorUserId))
            {
                return null;
            }

            var activeTab = string.Equals(tab, "categories", StringComparison.OrdinalIgnoreCase)
                ? "categories"
                : "equipment";

            var categoryData = await GetCategoriesAsync(actorUserId, categorySearch, categoryStatus, categoryPage);

            return new EquipmentManagementIndexViewModel
            {
                ActiveTab = activeTab,
                CategorySearch = categorySearch,
                CategoryStatusFilter = categoryStatus,
                CategoryPage = categoryData?.Page ?? 1,
                CategoryTotalPages = categoryData?.TotalPages ?? 1,
                CategoryTotalCount = categoryData?.TotalCount ?? 0,
                Categories = categoryData?.Categories ?? new List<CategoryViewModel>()
            };
        }
    }
}
