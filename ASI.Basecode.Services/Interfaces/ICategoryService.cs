using ASI.Basecode.Services.ServiceModels.Category;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<bool> CanManageAsync(string actorUserId);
        Task<CategoryResult> CreateCategoryAsync(string actorUserId, CreateCategoryViewModel model);
        Task<CategoryResult> UpdateCategoryAsync(string actorUserId, EditCategoryViewModel model);
        Task<CategoryResult> SetCategoryActiveAsync(string actorUserId, long categoryId, bool isActive);
        Task<CategoryIndexViewModel> GetCategoriesAsync(string actorUserId, string search = null, string status = null, int page = 1);
        Task<CategoryViewModel> GetCategoryByIdAsync(string actorUserId, long categoryId);
        Task<EquipmentManagementIndexViewModel> GetEquipmentManagementAsync(
            string actorUserId,
            string tab = "equipment",
            string categorySearch = null,
            string categoryStatus = null,
            int categoryPage = 1);
    }
}
