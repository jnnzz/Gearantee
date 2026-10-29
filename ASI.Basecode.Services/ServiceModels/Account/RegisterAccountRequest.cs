namespace ASI.Basecode.Services.ServiceModels.Account
{
    /// <summary>
    /// Public self-registration command. Free of UI attributes.
    /// </summary>
    public sealed class RegisterAccountRequest
    {
        public string UserCode { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
        public string Department { get; set; }
        public string ContactNumber { get; set; }
    }
}
