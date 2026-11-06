using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Enter your School ID.")]
        [Display(Name = "School ID")]
        [StringLength(256)]
        public string SchoolId { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required(ErrorMessage = "Enter a new password.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters long.")]
        [Display(Name = "New password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; }
    }

    public class VerifyPasswordResetOtpViewModel
    {
        [Required]
        [StringLength(256)]
        public string SchoolId { get; set; }

        [Required(ErrorMessage = "Enter the six-digit verification code.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the six-digit verification code.")]
        [Display(Name = "Verification code")]
        public string Otp { get; set; }
    }
}
