using ASI.Basecode.Services.ServiceModels.Account;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountService
    {
        /// <summary>
        /// Registers a public borrower account: identity creation, Borrower role
        /// assignment, and BorrowerProfile provisioning inside one atomic commit.
        /// Returns identity error descriptions on failure; nothing is persisted.
        /// </summary>
        Task<RegisterAccountResult> RegisterBorrowerAsync(
            RegisterAccountRequest request);
    }
}
