using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : ControllerBase<AccountController>
    {
        private const string InvalidLoginMessage =
            "Invalid user code/email or password.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AsiBasecodeDBContext _dbContext;
        private readonly SessionManager _sessionManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            AsiBasecodeDBContext dbContext,
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
            _roleManager = roleManager;
            _dbContext = dbContext;
            _sessionManager = new SessionManager(_session);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _sessionManager.Clear();
            _session.SetString("SessionId", Guid.NewGuid().ToString());
            ViewData["ReturnUrl"] = returnUrl;
            return View();
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

            var normalizedUserName = _userManager.NormalizeName(model.UserId);
            var normalizedEmail = _userManager.NormalizeEmail(model.UserId);
            var user = await _userManager.Users.SingleOrDefaultAsync(x =>
                x.NormalizedUserName == normalizedUserName ||
                x.NormalizedEmail == normalizedEmail ||
                x.UserCode == model.UserId);

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

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, InvalidLoginMessage);
                return View(model);
            }

            _session.SetString(
                "UserName",
                string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.UserCode
                    : user.DisplayName);

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
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

            if (await _userManager.FindByEmailAsync(model.Email) != null ||
                await _userManager.Users.AnyAsync(x =>
                    x.UserCode == model.UserCode))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "An account with that email or user code already exists.");
                return View(model);
            }

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync();

            var user = new ApplicationUser
            {
                UserName = model.UserCode,
                UserCode = model.UserCode,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(
                user,
                model.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                await transaction.RollbackAsync();
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync(
                DomainValues.Roles.Borrower))
            {
                var createRoleResult = await _roleManager.CreateAsync(
                    new IdentityRole(DomainValues.Roles.Borrower));
                if (!createRoleResult.Succeeded)
                {
                    AddIdentityErrors(createRoleResult);
                    await transaction.RollbackAsync();
                    return View(model);
                }
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                DomainValues.Roles.Borrower);
            if (!roleResult.Succeeded)
            {
                AddIdentityErrors(roleResult);
                await transaction.RollbackAsync();
                return View(model);
            }

            _dbContext.BorrowerProfiles.Add(new BorrowerProfile
            {
                UserId = user.Id,
                SchoolId = model.UserCode,
                Department = model.Department,
                ContactNumber = model.ContactNumber,
                IsEligible = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

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
            await _signInManager.SignOutAsync();
            _sessionManager.Clear();
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
