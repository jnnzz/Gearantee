using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace ASI.Basecode.Tests.Dashboard
{
    public sealed class DashboardAuthorizationTests
    {
        [Theory]
        [InlineData(nameof(DashboardController.Borrower), DomainValues.Roles.Borrower)]
        [InlineData(nameof(DashboardController.Custodian), DomainValues.Roles.Custodian)]
        [InlineData(nameof(DashboardController.Administrator), DomainValues.Roles.Administrator)]
        public void Dashboard_action_requires_its_role(string action, string role)
        {
            var attribute = typeof(DashboardController)
                .GetMethod(action)
                .GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>()
                .Single();

            Assert.Equal(role, attribute.Roles);
        }

        [Theory]
        [InlineData(new[] { DomainValues.Roles.Administrator, DomainValues.Roles.Borrower }, nameof(DashboardController.Administrator))]
        [InlineData(new[] { DomainValues.Roles.Custodian }, nameof(DashboardController.Custodian))]
        [InlineData(new[] { DomainValues.Roles.Borrower }, nameof(DashboardController.Borrower))]
        public void Index_routes_by_highest_role(string[] roles, string expectedAction)
        {
            var result = CreateController(roles).Index();

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(expectedAction, redirect.ActionName);
        }

        [Fact]
        public void Index_forbids_users_without_a_role()
        {
            var result = CreateController(System.Array.Empty<string>()).Index();

            Assert.IsType<ForbidResult>(result);
        }

        private static DashboardController CreateController(IEnumerable<string> roles)
        {
            var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    claims,
                    authenticationType: "test"))
            };
            var controller = new DashboardController(
                null,
                null,
                new HttpContextAccessor { HttpContext = context },
                NullLoggerFactory.Instance,
                new ConfigurationBuilder().Build())
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };
            return controller;
        }
    }
}
