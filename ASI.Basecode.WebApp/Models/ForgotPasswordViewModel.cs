using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    // WBS 9: ForgotPassword.cshtml posts this model to AccountController.ForgotPassword.
    // These annotations feed ModelState validation before the service receives a request.
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Enter your School ID.")]
        [Display(Name = "School ID")]
        [StringLength(256)]
        public string SchoolId { get; set; }
    }

    // WBS 12: created after successful OTP verification and posted by ResetPassword.cshtml.
    // UserId/Token identify the authorized reset; Identity validates that token on submission.
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

    // WBS 11: CheckInbox carries the School ID and entered code to VerifyPasswordResetOtp.
    // This checks input shape; PasswordResetOtpService and the controller check its validity.
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
