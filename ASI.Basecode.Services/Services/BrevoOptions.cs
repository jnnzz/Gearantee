namespace ASI.Basecode.Services.Services
{
    public sealed class BrevoOptions
    {
        public const string SectionName = "Brevo";

        public string ApiKey { get; set; }

        public string BaseUrl { get; set; } = "https://api.brevo.com";

        public string SenderEmail { get; set; }

        public string SenderName { get; set; } = "Gearantee";
    }
}
