using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IBorrowerProfileRepository
    {
        IQueryable<BorrowerProfile> GetBorrowerProfiles();
    }
}
