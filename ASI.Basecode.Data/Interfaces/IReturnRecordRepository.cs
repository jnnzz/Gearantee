using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReturnRecordRepository
    {
        IQueryable<ReturnRecord> GetReturnRecords();
        void AddReturnRecord(ReturnRecord model);
    }
}
