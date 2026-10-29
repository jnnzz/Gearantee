using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : ServiceBase, IAccountService
    {
        private readonly AsiBasecodeDBContext _db;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _db = unitOfWork.Context;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<RegisterAccountResult> RegisterBorrowerAsync(
            RegisterAccountRequest request)
        {
            if (request == null)
            {
                return RegisterAccountResult.Failure(
                    new[] { "The registration form was incomplete." });
            }

            if (await _userManager.FindByEmailAsync(request.Email) != null ||
                await _userManager.Users.AnyAsync(x =>
                    x.UserCode == request.UserCode))
            {
                return RegisterAccountResult.Failure(new[]
                {
                    "An account with that email or user code already exists."
                });
            }

            return await _db.Database.CreateExecutionStrategy()
                .ExecuteAsync(async () =>
            {
                // Rebuild state per attempt so a rolled-back attempt cannot leave a duplicate
                // tracked user/profile behind for the retry.
                _db.ChangeTracker.Clear();

                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var user = new ApplicationUser
                {
                    UserName = request.UserCode,
                    UserCode = request.UserCode,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(
                    user,
                    request.Password);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return RegisterAccountResult.Failure(
                        createResult.Errors.Select(error => error.Description));
                }

                if (!await _roleManager.RoleExistsAsync(
                        DomainValues.Roles.Borrower))
                {
                    var createRoleResult = await _roleManager.CreateAsync(
                        new IdentityRole(DomainValues.Roles.Borrower));
                    if (!createRoleResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return RegisterAccountResult.Failure(
                            createRoleResult.Errors.Select(error => error.Description));
                    }
                }

                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    DomainValues.Roles.Borrower);
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return RegisterAccountResult.Failure(
                        roleResult.Errors.Select(error => error.Description));
                }

                _db.BorrowerProfiles.Add(new BorrowerProfile
                {
                    UserId = user.Id,
                    Department = request.Department,
                    ContactNumber = request.ContactNumber,
                    IsEligible = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return RegisterAccountResult.Success();
            });
        }
    }
}
