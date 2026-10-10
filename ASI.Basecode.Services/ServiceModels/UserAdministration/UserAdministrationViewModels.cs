using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.UserAdministration
{
    public class UserAccountsIndexViewModel
    {
        public string Search { get; set; }
        public string StatusFilter { get; set; }
        public string RoleFilter { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public IReadOnlyList<UserAccountRowViewModel> Users { get; set; } =
            new List<UserAccountRowViewModel>();
        public IReadOnlyList<RoleOptionViewModel> Roles { get; set; } =
            new List<RoleOptionViewModel>();
    }

    public class UserAccountRowViewModel
    {
        public string Id { get; set; }
        public string UserCode { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = new List<string>();
        public bool IsActive { get; set; }
    }

    public class RoleOptionViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "User code / school ID")]
        public string UserCode { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Initial role")]
        public string RoleName { get; set; } = "Borrower";

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }

        [StringLength(100)]
        [Display(Name = "School ID")]
        public string SchoolId { get; set; }

        [StringLength(200)]
        public string Department { get; set; }

        [Phone, StringLength(50)]
        [Display(Name = "Contact number")]
        public string ContactNumber { get; set; }
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }
        public string UserCode { get; set; }
        public string ConcurrencyStamp { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; }

        public List<string> SelectedRoles { get; set; } = new List<string>();
        public IReadOnlyList<RoleOptionViewModel> AvailableRoles { get; set; } =
            new List<RoleOptionViewModel>();

        [StringLength(100)]
        [Display(Name = "School ID")]
        public string SchoolId { get; set; }

        [StringLength(200)]
        public string Department { get; set; }

        [Phone, StringLength(50)]
        [Display(Name = "Contact number")]
        public string ContactNumber { get; set; }

        public bool IsActive { get; set; }
    }

    public class RolePermissionsViewModel
    {
        public IReadOnlyList<RoleOptionViewModel> Roles { get; set; } =
            new List<RoleOptionViewModel>();
        public IReadOnlyDictionary<string, string> RoleStamps { get; set; } =
            new Dictionary<string, string>();
        public IReadOnlyList<PermissionRowViewModel> Permissions { get; set; } =
            new List<PermissionRowViewModel>();
    }

    public class PermissionRowViewModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> GrantedRoleIds { get; set; } =
            new List<string>();
        public bool IsLocked { get; set; }
    }

    public class UpdateRolePermissionsViewModel
    {
        public Dictionary<string, string> RoleStamps { get; set; } =
            new Dictionary<string, string>();
        public List<PermissionSelectionViewModel> Assignments { get; set; } =
            new List<PermissionSelectionViewModel>();
    }

    public class PermissionSelectionViewModel
    {
        [Required]
        public string PermissionName { get; set; }
        public List<string> RoleIds { get; set; } = new List<string>();
    }

    public class SetAccountActiveViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string ConcurrencyStamp { get; set; }

        public bool IsActive { get; set; }
    }

    public class UserAdministrationResult
    {
        public bool Succeeded { get; private set; }
        public bool Forbidden { get; private set; }
        public bool NotFound { get; private set; }
        public bool Conflict { get; private set; }
        public string Message { get; private set; }

        public static UserAdministrationResult Success(string message = null) =>
            new UserAdministrationResult { Succeeded = true, Message = message };

        public static UserAdministrationResult Failure(string message) =>
            new UserAdministrationResult { Message = message };

        public static UserAdministrationResult AccessDenied() =>
            new UserAdministrationResult { Forbidden = true };

        public static UserAdministrationResult Missing() =>
            new UserAdministrationResult { NotFound = true };

        public static UserAdministrationResult Stale(string message) =>
            new UserAdministrationResult { Conflict = true, Message = message };
    }
}
