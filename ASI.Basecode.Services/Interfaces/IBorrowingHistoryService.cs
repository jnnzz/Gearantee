using ASI.Basecode.Services.ServiceModels.BorrowingHistory;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBorrowingHistoryService
    {
        Task<BorrowingHistoryIndexViewModel> GetHistoryAsync(
            BorrowingHistoryFilterViewModel filter,
            string userId,
            bool onlyOwnHistory,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<BorrowingHistoryRowViewModel> StreamReportRowsAsync(
            BorrowingHistoryFilterViewModel filter,
            string userId,
            bool onlyOwnHistory,
            CancellationToken cancellationToken = default);
    }
}
