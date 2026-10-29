using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels.AvailabilityCalendar;
using ASI.Basecode.WebApp.Authorization;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests.AvailabilityCalendar
{
    public sealed class AvailabilityCalendarAuthorizationTests
    {
        [Theory]
        [InlineData(DomainValues.Roles.Borrower)]
        [InlineData(DomainValues.Roles.Custodian)]
        [InlineData(DomainValues.Roles.Administrator)]
        public async Task Calendar_and_event_feed_require_equipment_browse(string role)
        {
            Assert.True(await CanAccess(nameof(AvailabilityCalendarController.Index), role, true));
            Assert.True(await CanAccess(nameof(AvailabilityCalendarController.Events), role, true));
            Assert.False(await CanAccess(nameof(AvailabilityCalendarController.Index), role, false));
            Assert.False(await CanAccess(nameof(AvailabilityCalendarController.Events), role, false));
        }

        [Fact]
        public void Calendar_responses_are_not_cached()
        {
            var attribute = typeof(AvailabilityCalendarController)
                .GetCustomAttribute<ResponseCacheAttribute>();

            Assert.NotNull(attribute);
            Assert.True(attribute.NoStore);
            Assert.Equal(ResponseCacheLocation.None, attribute.Location);
        }

        [Fact]
        public void Invalid_calendar_date_is_cleared_before_rendering()
        {
            var context = new DefaultHttpContext();
            var controller = new AvailabilityCalendarController(
                new HttpContextAccessor { HttpContext = context },
                NullLoggerFactory.Instance,
                new ConfigurationBuilder().Build(),
                new EmptyCalendarService())
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };

            var result = Assert.IsType<ViewResult>(controller.Index(
                new AvailabilityCalendarFilterViewModel { Date = "2026-02-31" }));
            var model = Assert.IsType<AvailabilityCalendarPageViewModel>(result.Model);

            Assert.Null(model.Filter.Date);
            Assert.Contains("invalid", model.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> CanAccess(string action, string role, bool hasBrowse)
        {
            var method = typeof(AvailabilityCalendarController).GetMethods()
                .Single(candidate => candidate.Name == action &&
                    candidate.GetCustomAttributes<HttpGetAttribute>(true).Any());
            var authorizeData = typeof(AvailabilityCalendarController)
                .GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true))
                .ToList();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(options => options.AddPolicy(
                DomainValues.Permissions.EquipmentBrowse,
                policy => policy.RequireClaim(
                    ApplicationClaimsPrincipalFactory.PermissionClaimType,
                    DomainValues.Permissions.EquipmentBrowse)));
            using var provider = services.BuildServiceProvider();
            var policy = await AuthorizationPolicy.CombineAsync(
                provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);
            Assert.NotNull(policy);

            var claims = new[] { new Claim(ClaimTypes.Role, role) }.AsEnumerable();
            if (hasBrowse)
            {
                claims = claims.Append(new Claim(
                    ApplicationClaimsPrincipalFactory.PermissionClaimType,
                    DomainValues.Permissions.EquipmentBrowse));
            }

            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return (await provider.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(user, policy)).Succeeded;
        }

        private sealed class EmptyCalendarService : IAvailabilityCalendarService
        {
            public AvailabilityCalendarPageViewModel GetCalendarPage(
                AvailabilityCalendarFilterViewModel filter) =>
                new() { Filter = filter };

            public AvailabilityCalendarDataViewModel GetCalendarData(
                AvailabilityCalendarFilterViewModel filter,
                DateTimeOffset start,
                DateTimeOffset end) => new();
        }
    }
}
