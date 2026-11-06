using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ASI.Basecode.Tests.UserAdministration
{
    public class UserAdministrationAuthorizationTests
    {
        [Fact]
        public void UsersController_RequiresAdministratorAndUserRolePermission()
        {
            var authorization = typeof(UsersController)
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Single();

            Assert.Equal(DomainValues.Roles.Administrator, authorization.Roles);
            Assert.Equal(DomainValues.Permissions.UserRoleManage, authorization.Policy);
        }

        [Theory]
        [InlineData(nameof(UsersController.Create))]
        [InlineData(nameof(UsersController.Import))]
        [InlineData(nameof(UsersController.Edit))]
        [InlineData(nameof(UsersController.SetActive))]
        [InlineData(nameof(UsersController.Permissions))]
        public void MutatingActions_RequireAntiforgery(string actionName)
        {
            var method = typeof(UsersController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == actionName &&
                    candidate.GetCustomAttribute<HttpPostAttribute>() != null);

            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }
}
