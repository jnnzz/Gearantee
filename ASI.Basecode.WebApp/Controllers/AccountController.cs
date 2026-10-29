using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Account;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : ControllerBase<AccountController>
    {
        private const string InvalidLoginMessage =
            "Invalid user code/email or password.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAccountService _accountService;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly IPasswordResetOtpService _passwordResetOtpService;

        private const string PasswordResetGenericMessage =
            "The verification code is invalid or has expired.";

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAccountService accountService,
            IPasswordHasher<ApplicationUser> passwordHasher,
            IPasswordResetOtpService passwordResetOtpService,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
            : base(
                httpContextAccessor,
                loggerFactory,
                configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _accountService = accountService;
            _passwordHasher = passwordHasher;
            _passwordResetOtpService = passwordResetOtpService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(PasswordResetOtpService.RequestRateLimitPolicyName)]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && user.IsActive)
            {
                // Anti-enumeration: the response is identical whether or not the
                // account exists; the service handles reservation, storage, email,
                // and failure rollback internally.
                await _passwordResetOtpService.IssueOtpAsync(user, email);
            }

            TempData["PasswordResetEmail"] = email;
            return RedirectToAction(nameof(ForgotPasswordCheckInbox));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordCheckInbox()
        {
            if (TempData["PasswordResetEmail"] is not string email ||
                string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new VerifyPasswordResetOtpViewModel { Email = email });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(PasswordResetOtpService.VerifyRateLimitPolicyName)]
        public async Task<IActionResult> VerifyPasswordResetOtp(
            VerifyPasswordResetOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(ForgotPasswordCheckInbox), model);
            }

            var email = model.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    PasswordResetGenericMessage);
                return View(nameof(ForgotPasswordCheckInbox), model);
            }

            var reservation = await _passwordResetOtpService
                .TryReserveAttemptAsync(user.Id, DateTimeOffset.UtcNow);
            if (!reservation.IsReserved)
            {
                ModelState.AddModelError(
                    string.Empty,
                    reservation.Status == PasswordResetOtpAttemptStatus.TooManyAttempts
                        ? "Too many attempts. Request a new verification code."
                        : PasswordResetGenericMessage);
                return View(nameof(ForgotPasswordCheckInbox), model);
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                reservation.State.OtpHash,
                model.Otp.Trim());
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                if (reservation.State.Attempts >=
                    PasswordResetOtpService.MaximumAttempts)
                {
                    if (!await _passwordResetOtpService.TryConsumeAsync(
                            user.Id,
                            reservation.ReservedValue))
                    {
                        _logger.LogWarning(
                            "Unable to consume an exhausted password reset OTP for user {UserCode}.",
                            user.UserCode);
                    }
                }

                ModelState.AddModelError(
                    string.Empty,
                    reservation.State.Attempts >=
                        PasswordResetOtpService.MaximumAttempts
                        ? "Too many attempts. Request a new verification code."
                        : PasswordResetGenericMessage);
                return View(nameof(ForgotPasswordCheckInbox), model);
            }

            if (!await _passwordResetOtpService.TryConsumeAsync(
                    user.Id,
                    reservation.ReservedValue))
            {
                ModelState.AddModelError(
                    string.Empty,
                    PasswordResetGenericMessage);
                return View(nameof(ForgotPasswordCheckInbox), model);
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            return View(
                nameof(ResetPassword),
                new ResetPasswordViewModel
                {
                    UserId = user.Id,
                    Token = resetToken
                });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This password-reset session is invalid or has expired.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.Password);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(model);
            }

            var resetFailedAccessResult =
                await _userManager.ResetAccessFailedCountAsync(user);
            if (!resetFailedAccessResult.Succeeded)
            {
                _logger.LogWarning(
                    "Password reset succeeded but failed to clear the failed-login count for user {UserCode}.",
                    user.UserCode);
            }

            var clearLockoutResult = await _userManager.SetLockoutEndDateAsync(
                user,
                null);
            if (!clearLockoutResult.Succeeded)
            {
                _logger.LogWarning(
                    "Password reset succeeded but failed to clear lockout for user {UserCode}.",
                    user.UserCode);
            }

            TempData["SuccessMessage"] =
                "Your password was reset. You can now sign in with your new password.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = model.UserId?.Trim();
            var normalizedUserName = _userManager.NormalizeName(login);
            var normalizedEmail = _userManager.NormalizeEmail(login);
            var user = await _userManager.Users.FirstOrDefaultAsync(x =>
                x.NormalizedUserName == normalizedUserName ||
                x.NormalizedEmail == normalizedEmail ||
                x.UserCode == login);

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, InvalidLoginMessage);
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Too many failed attempts. Your account is locked for 15 minutes.");
                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, InvalidLoginMessage);
                return View(model);
            }

            _logger.LogInformation("User {UserCode} signed in.", user.UserCode);

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountService.RegisterBorrowerAsync(
                new RegisterAccountRequest
                {
                    UserCode = model.UserCode,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Password = model.Password,
                    Department = model.Department,
                    ContactNumber = model.ContactNumber
                });

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
                return View(model);
            }

            TempData["SuccessMessage"] =
                "Account created. An administrator must confirm borrowing eligibility.";
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOutUser()
        {
            var userCode = User.FindFirst("user_code")?.Value ?? User.Identity?.Name;
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            _logger.LogInformation("User {UserCode} signed out.", userCode);
            return RedirectToAction(nameof(Login));
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

    }
}
