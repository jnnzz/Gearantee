using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Locks
{
    /// <summary>
    /// Provider-specific row locks over Identity role rows.
    /// </summary>
    public static class RoleRowLock
    {
        /// <summary>
        /// Locks the Administrator role row (SQL Server UPDLOCK, ROWLOCK) to serialize
        /// concurrent demotions/deactivations and prevent the race where concurrent
        /// administrators demote or deactivate each other. On non-SQL Server providers
        /// this is a plain read.
        /// </summary>
        public static async Task LockAdministratorRowAsync(AsiBasecodeDBContext db)
        {
            if (db.Database.IsSqlServer())
            {
                await db.Roles
                    .FromSqlInterpolated(
                        $"SELECT * FROM [AspNetRoles] WITH (UPDLOCK, ROWLOCK) WHERE [NormalizedName] = 'ADMINISTRATOR'")
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();
            }
        }
    }
}
