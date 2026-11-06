using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data
{

    /// <summary>
    /// Unit of Work Implementation
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        /// <summary>
        /// Gets the database context
        /// </summary>
        public DbContext Database { get; private set; }

        /// <summary>
        /// The typed application context.
        /// </summary>
        public AsiBasecodeDBContext Context => (AsiBasecodeDBContext)Database;

        /// <summary>
        /// Initializes a new instance of the UnitOfWork class.
        /// </summary>
        /// <param name="serviceContext">The service context.</param>
        public UnitOfWork(AsiBasecodeDBContext serviceContext)
        {
            Database = serviceContext;
        }

        /// <summary>
        /// Saves the changes to database
        /// </summary>
        public void SaveChanges()
        {
            Database.SaveChanges();
        }

        /// <summary>
        /// Persists all staged changes as one atomic unit.
        /// </summary>
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Database.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Database.Dispose();
        }
    }
}
