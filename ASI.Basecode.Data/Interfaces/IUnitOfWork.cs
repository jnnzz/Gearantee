using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Unit of Work Interface
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Gets the database context
        /// </summary>
        /// <value>
        /// The database.
        /// </value>
        DbContext Database { get; }

        /// <summary>
        /// The typed application context. Resolved here in the data layer so services
        /// never cast <see cref="Database"/> to the concrete type themselves.
        /// </summary>
        AsiBasecodeDBContext Context { get; }

        /// <summary>
        /// Saves the changes to database
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Persists all staged changes as one atomic unit.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
