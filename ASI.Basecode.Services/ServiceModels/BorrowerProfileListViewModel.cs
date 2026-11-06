using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// One page of borrower profiles. Mirrors the shape of UserAccountsIndexViewModel so the
    /// list stays bounded as the campus account list grows (PERF-01).
    /// </summary>
    public class BorrowerProfileListViewModel
    {
        public IReadOnlyList<BorrowerProfileViewModel> Borrowers { get; set; } =
            new List<BorrowerProfileViewModel>();

        public string Search { get; set; }

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int TotalCount { get; set; }
    }
}
