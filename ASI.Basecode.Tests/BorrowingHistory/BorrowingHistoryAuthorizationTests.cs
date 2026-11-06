using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Authorization;
using System;
using Xunit;

namespace ASI.Basecode.Tests.BorrowingHistory
{
    public sealed class BorrowingHistoryAuthorizationTests
    {
        [Fact]
        public void Controller_requires_history_view_permission()
        {
            var authorization = (AuthorizeAttribute)Attribute.GetCustomAttribute(
                typeof(BorrowingHistoryController),
                typeof(AuthorizeAttribute));

            Assert.NotNull(authorization);
            Assert.Equal(DomainValues.Permissions.HistoryView, authorization.Policy);
        }
    }
}
