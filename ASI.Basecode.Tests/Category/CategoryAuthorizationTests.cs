using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ASI.Basecode.Tests.Category
{
    public class CategoryAuthorizationTests
    {
        [Fact]
        public void CategoriesController_RequiresAdministratorAndEquipmentManagePermission()
        {
            var authorization = typeof(CategoriesController)
                .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Single();

            Assert.Equal(DomainValues.Roles.Administrator, authorization.Roles);
            Assert.Equal(DomainValues.Permissions.EquipmentManage, authorization.Policy);
        }

        [Fact]
        public void CategoriesController_HasNoCacheAttribute()
        {
            var cache = typeof(CategoriesController)
                .GetCustomAttribute<ResponseCacheAttribute>();

            Assert.NotNull(cache);
            Assert.True(cache.NoStore);
            Assert.Equal(ResponseCacheLocation.None, cache.Location);
        }

        [Theory]
        [InlineData(nameof(CategoriesController.Create))]
        [InlineData(nameof(CategoriesController.Edit))]
        [InlineData(nameof(CategoriesController.SetActive))]
        public void MutatingActions_RequireAntiforgery(string actionName)
        {
            var method = typeof(CategoriesController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == actionName &&
                    candidate.GetCustomAttribute<HttpPostAttribute>() != null);

            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }
}
