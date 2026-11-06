using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Category
{
    public class CreateCategoryViewModel
    {
        [Required(ErrorMessage = "Category code is required.")]
        [StringLength(50, ErrorMessage = "Category code cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Category code may only contain letters, numbers, hyphens, and underscores.")]
        [Display(Name = "Category code")]
        public string CategoryCode { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
        [Display(Name = "Category name")]
        public string CategoryName { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditCategoryViewModel
    {
        [Required]
        public long CategoryId { get; set; }

        [Required(ErrorMessage = "Category code is required.")]
        [StringLength(50, ErrorMessage = "Category code cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Category code may only contain letters, numbers, hyphens, and underscores.")]
        [Display(Name = "Category code")]
        public string CategoryCode { get; set; }

        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
        [Display(Name = "Category name")]
        public string CategoryName { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    public class SetCategoryActiveModel
    {
        [Required]
        public long CategoryId { get; set; }

        public bool IsActive { get; set; }
    }

    public class CategoryViewModel
    {
        public long CategoryId { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CategoryIndexViewModel
    {
        public string Search { get; set; }
        public string StatusFilter { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public IReadOnlyList<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
    }

    public class EquipmentManagementIndexViewModel
    {
        public string ActiveTab { get; set; } = "equipment";
        public ASI.Basecode.Services.ServiceModels.EquipmentItem.EquipmentItemIndexViewModel Equipment { get; set; } =
            new ASI.Basecode.Services.ServiceModels.EquipmentItem.EquipmentItemIndexViewModel();
        public string CategorySearch { get; set; }
        public string CategoryStatusFilter { get; set; }
        public int CategoryPage { get; set; } = 1;
        public int CategoryTotalPages { get; set; } = 1;
        public int CategoryTotalCount { get; set; }
        public IReadOnlyList<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
    }

    public class CategoryResult
    {
        public bool Succeeded { get; set; }
        public bool Forbidden { get; set; }
        public string Message { get; set; }
        public long CategoryId { get; set; }

        public static CategoryResult Success(string message, long id = 0) =>
            new() { Succeeded = true, Message = message, CategoryId = id };

        public static CategoryResult Failure(string message) =>
            new() { Succeeded = false, Message = message };

        public static CategoryResult AccessDenied(string message = "You do not have permission to manage equipment categories.") =>
            new() { Forbidden = true, Message = message };
    }
}
