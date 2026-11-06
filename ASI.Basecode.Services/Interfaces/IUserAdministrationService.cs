using ASI.Basecode.Services.ServiceModels.UserAdministration;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IUserAdministrationService
    {
        Task<UserImportResult> ImportAsync(string actorUserId,
            System.Collections.Generic.IReadOnlyList<UserImportRow> rows);
        Task<bool> CanManageAsync(string actorUserId);
        Task<UserAccountsIndexViewModel> GetAccountsAsync(
            string actorUserId,
            string search,
            string status,
            string roleId,
            int page);
        Task<EditUserViewModel> GetUserAsync(string actorUserId, string userId);
        Task<RolePermissionsViewModel> GetRolePermissionsAsync(
            string actorUserId);
        Task<UserAdministrationResult> CreateAsync(
            string actorUserId,
            CreateUserViewModel model);
        Task<UserAdministrationResult> UpdateAsync(
            string actorUserId,
            EditUserViewModel model);
        Task<UserAdministrationResult> SetActiveAsync(
            string actorUserId,
            SetAccountActiveViewModel model);
        Task<UserAdministrationResult> UpdatePermissionsAsync(
            string actorUserId,
            UpdateRolePermissionsViewModel model);
    }
}
