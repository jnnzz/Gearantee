using ASI.Basecode.Services.ServiceModels.EquipmentItem;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentItemService
    {
        Task<EquipmentItemIndexViewModel> GetEquipmentItemsAsync(
            string actorUserId,
            string search = null,
            string status = null,
            long? categoryId = null,
            int page = 1);

        Task<EquipmentItemFormViewModel> GetCreateFormAsync(string actorUserId);
        Task<EquipmentItemFormViewModel> GetEditFormAsync(string actorUserId, long equipmentId);
        Task<string> GetImageUrlAsync(string actorUserId, long equipmentId);
        Task<EquipmentItemOperationResult> CreateAsync(
            string actorUserId,
            EquipmentItemFormViewModel model);
        Task<EquipmentItemOperationResult> UpdateAsync(
            string actorUserId,
            EquipmentItemFormViewModel model);
        Task<EquipmentItemOperationResult> SetArchivedAsync(
            string actorUserId,
            long equipmentId,
            bool isArchived);
    }
}
