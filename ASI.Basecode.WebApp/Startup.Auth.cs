using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.WebApp.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ASI.Basecode.WebApp
{
    internal partial class StartupConfigurer
    {
        private void ConfigureIdentityAndAuthorization()
        {
            _services
                .AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;

                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = false;
                })
                .AddEntityFrameworkStores<AsiBasecodeDBContext>()
                .AddDefaultTokenProviders();

            _services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "Gearantee.Identity";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy =
                    Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(2);
                options.SlidingExpiration = true;
            });

            _services.AddScoped<
                IUserClaimsPrincipalFactory<ApplicationUser>,
                ApplicationClaimsPrincipalFactory>();

            _services.AddAuthorization(options =>
            {
                AddPermissionPolicy(options, DomainValues.Permissions.EquipmentBrowse);
                AddPermissionPolicy(options, DomainValues.Permissions.ReservationCreate);
                AddPermissionPolicy(options, DomainValues.Permissions.ReservationReview);
                AddPermissionPolicy(options, DomainValues.Permissions.TransactionReleaseReturn);
                AddPermissionPolicy(options, DomainValues.Permissions.EquipmentManage);
                AddPermissionPolicy(options, DomainValues.Permissions.BorrowerManage);
                AddPermissionPolicy(options, DomainValues.Permissions.UserRoleManage);
                AddPermissionPolicy(options, DomainValues.Permissions.HistoryView);
            });

            _services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            });
        }

        private static void AddPermissionPolicy(
            AuthorizationOptions options,
            string permission)
        {
            options.AddPolicy(
                permission,
                policy => policy.RequireClaim(
                    ApplicationClaimsPrincipalFactory.PermissionClaimType,
                    permission));
        }
    }
}
