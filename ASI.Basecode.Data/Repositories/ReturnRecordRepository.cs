using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ReturnRecordRepository : BaseRepository, IReturnRecordRepository
    {
        public ReturnRecordRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public IQueryable<ReturnRecord> GetReturnRecords()
        {
            return this.GetDbSet<ReturnRecord>();
        }

        public void AddReturnRecord(ReturnRecord model)
        {
            this.GetDbSet<ReturnRecord>().Add(model);
            UnitOfWork.SaveChanges();
        }
    }
}
