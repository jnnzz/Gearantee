using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Utilities;
using System.Reflection;
using Xunit;

namespace ASI.Basecode.Tests.BorrowerProfiles
{
    /// <summary>
    /// AUTH-01 requires a sensitive write to be coordinated with concurrent permission changes.
    /// The coordination is the shared SQL Server application lock: the borrower-profile write
    /// path and the user-administration permission/account paths must take the <em>same</em>
    /// resource, otherwise a revocation could still land between the permission check and the
    /// commit. This guards the two paths against drifting apart.
    /// </summary>
    public class AdministrationLockTests
    {
        [Fact]
        public void UserAdministration_TakesTheSameApplicationLockAsTheProfileWritePath()
        {
            var field = typeof(UserAdministrationService).GetField(
                "AdministrationLockName",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(field);
            Assert.Equal(AdministrationLock.ResourceName, field.GetValue(null));
        }
    }
}
