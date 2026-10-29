using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.EquipmentItem
{
    public class EquipmentItemIndexViewModel
    {
        public string Search { get; set; }
        public string StatusFilter { get; set; }
        public long? CategoryId { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public IReadOnlyList<EquipmentItemRowViewModel> Items { get; set; } =
            new List<EquipmentItemRowViewModel>();
        public IReadOnlyList<EquipmentCategoryOptionViewModel> Categories { get; set; } =
            new List<EquipmentCategoryOptionViewModel>();
    }

    public class EquipmentItemRowViewModel
    {
        public long EquipmentId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string Location { get; set; }
        public string ConditionStatus { get; set; }
        public string ItemStatus { get; set; }
        public bool IsArchived { get; set; }
        public bool IsLocked { get; set; }
    }

    public class EquipmentItemFormViewModel
    {
        public long EquipmentId { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Choose an equipment category.")]
        [Display(Name = "Category")]
        public long CategoryId { get; set; }

        [Required(ErrorMessage = "Item code is required.")]
        [StringLength(100, ErrorMessage = "Item code cannot exceed 100 characters.")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "Use letters, numbers, periods, hyphens, or underscores for the item code.")]
        [Display(Name = "Item code")]
        public string ItemCode { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        [StringLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
        [Display(Name = "Item name")]
        public string ItemName { get; set; }

        [StringLength(4000, ErrorMessage = "Description cannot exceed 4,000 characters.")]
        public string Description { get; set; }

        [StringLength(500, ErrorMessage = "Catalog image URL cannot exceed 500 characters.")]
        [Display(Name = "Catalog image URL")]
        public string ImageUrl { get; set; }

        [StringLength(150, ErrorMessage = "Brand cannot exceed 150 characters.")]
        public string Brand { get; set; }

        [StringLength(150, ErrorMessage = "Model cannot exceed 150 characters.")]
        [Display(Name = "Model")]
        public string ModelName { get; set; }

        [StringLength(200, ErrorMessage = "Serial number cannot exceed 200 characters.")]
        [Display(Name = "Serial number")]
        public string SerialNumber { get; set; }

        [Required(ErrorMessage = "Storage location is required.")]
        [StringLength(200, ErrorMessage = "Storage location cannot exceed 200 characters.")]
        [Display(Name = "Storage location")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Choose the equipment condition.")]
        [Display(Name = "Condition")]
        public string ConditionStatus { get; set; } = DomainValues.ReturnConditions.Good;

        [Required(ErrorMessage = "Choose the equipment status.")]
        [Display(Name = "Status")]
        public string ItemStatus { get; set; } = DomainValues.EquipmentStatuses.Available;

        public bool IsLocked { get; set; }
        public bool IsCategoryInactive { get; set; }
        public IReadOnlyList<EquipmentCategoryOptionViewModel> Categories { get; set; } =
            new List<EquipmentCategoryOptionViewModel>();
    }

    public class EquipmentCategoryOptionViewModel
    {
        public long CategoryId { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public bool IsActive { get; set; }
    }

    public class EquipmentItemOperationResult
    {
        public bool Succeeded { get; private set; }
        public bool Forbidden { get; private set; }
        public bool Missing { get; private set; }
        public string Message { get; private set; }
        public long EquipmentId { get; private set; }

        public static EquipmentItemOperationResult Success(string message, long id) =>
            new() { Succeeded = true, Message = message, EquipmentId = id };

        public static EquipmentItemOperationResult Failure(string message) =>
            new() { Message = message };

        public static EquipmentItemOperationResult AccessDenied() =>
            new() { Forbidden = true, Message = "You do not have permission to manage equipment items." };

        public static EquipmentItemOperationResult NotFound() =>
            new() { Missing = true, Message = "Equipment item not found." };
    }
}
