using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class BorrowerProfileRepository : BaseRepository, IBorrowerProfileRepository
    {
        public BorrowerProfileRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<BorrowerProfile> GetBorrowerProfiles()
        {
            return this.GetDbSet<BorrowerProfile>();
        }
    }
}
