namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerProfileViewModel
    {
        public long BorrowerProfileId { get; set; }
        public string SchoolId { get; set; }
        public string BorrowerName { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
        public bool IsEligible { get; set; }

        /// <summary>
        /// The row's optimistic-concurrency token, carried so the edit form can post back the
        /// version it was rendered from.
        /// </summary>
        public string ConcurrencyStamp { get; set; }
    }
}
