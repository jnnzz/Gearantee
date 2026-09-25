using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Authorization
{
    public class ApplicationClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public const string PermissionClaimType = "permission";

        private readonly AsiBasecodeDBContext _dbContext;

        public ApplicationClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            AsiBasecodeDBContext dbContext)
            : base(userManager, roleManager, optionsAccessor)
        {
            _dbContext = dbContext;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
            ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            var roleNames = await UserManager.GetRolesAsync(user);

            var permissions = await (
                from role in _dbContext.Roles
                join rolePermission in _dbContext.RolePermissions
                    on role.Id equals rolePermission.RoleId
                join permission in _dbContext.Permissions
                    on rolePermission.PermissionId equals permission.PermissionId
                where roleNames.Contains(role.Name)
                select permission.PermissionName)
                .Distinct()
                .ToListAsync();

            foreach (var permission in permissions)
            {
                identity.AddClaim(new Claim(PermissionClaimType, permission));
            }

            identity.AddClaim(new Claim("user_code", user.UserCode));
            return identity;
        }
    }
}
