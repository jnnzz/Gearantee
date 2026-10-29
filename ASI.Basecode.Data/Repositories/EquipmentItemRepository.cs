using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class EquipmentItemRepository : BaseRepository, IEquipmentItemRepository
    {
        public EquipmentItemRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<EquipmentItem> GetEquipmentItems()
        {
            return this.GetDbSet<EquipmentItem>();
        }

        public EquipmentItem LoadForUpdate(long equipmentId, bool includeArchived = false)
        {
            EnsureSerializableTransaction();
            return BuildForUpdateQuery(equipmentId, includeArchived).SingleOrDefault();
        }

        public async Task<EquipmentItem> LoadForUpdateAsync(
            long equipmentId,
            bool includeArchived = false)
        {
            EnsureSerializableTransaction();
            return await BuildForUpdateQuery(equipmentId, includeArchived)
                .SingleOrDefaultAsync();
        }

        private IQueryable<EquipmentItem> BuildForUpdateQuery(
            long equipmentId,
            bool includeArchived)
        {
            if (Context.Database.IsSqlServer())
            {
                return includeArchived
                    ? Context.EquipmentItems.FromSqlInterpolated($"""
                        SELECT *
                        FROM [EquipmentItem] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        WHERE [EquipmentId] = {equipmentId}
                        """)
                    : Context.EquipmentItems.FromSqlInterpolated($"""
                        SELECT *
                        FROM [EquipmentItem] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        WHERE [EquipmentId] = {equipmentId} AND [IsArchived] = 0
                        """);
            }

            return Context.EquipmentItems.Where(item =>
                item.EquipmentId == equipmentId &&
                (includeArchived || !item.IsArchived));
        }

        private void EnsureSerializableTransaction()
        {
            var transaction = Context.Database.CurrentTransaction;
            if (transaction == null ||
                transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            {
                throw new InvalidOperationException(
                    "Equipment must be locked inside an active SERIALIZABLE transaction.");
            }
        }
    }
}
