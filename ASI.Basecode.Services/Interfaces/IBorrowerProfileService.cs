using ASI.Basecode.Services.ServiceModels;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBorrowerProfileService
    {
        Task<BorrowerProfileListViewModel> GetBorrowerProfilesAsync(string search, int page);
        Task<BorrowerProfileViewModel> GetBorrowerProfileAsync(long id);

        /// <summary>
        /// Write paths take the acting user so the service can re-check current database
        /// permissions rather than trusting the sign-in cookie's permission claim.
        /// </summary>
        Task<BorrowerProfileResult> UpdateBorrowerProfileAsync(
            string actorUserId,
            long id,
            BorrowerProfileUpdateViewModel model);

        Task<BorrowerProfileResult> UpdateEligibilityAsync(
            string actorUserId,
            long id,
            bool isEligible);
    }
}
