using ASI.Basecode.Data.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEquipmentItemRepository
    {
        IQueryable<EquipmentItem> GetEquipmentItems();

        /// <summary>
        /// Locks a single equipment row for update (SQL Server UPDLOCK/HOLDLOCK/ROWLOCK)
        /// and returns it. Must be called inside an active SERIALIZABLE transaction.
        /// </summary>
        EquipmentItem LoadForUpdate(long equipmentId, bool includeArchived = false);

        Task<EquipmentItem> LoadForUpdateAsync(
            long equipmentId,
            bool includeArchived = false);
    }
}
