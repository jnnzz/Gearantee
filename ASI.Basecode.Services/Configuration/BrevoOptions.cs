namespace ASI.Basecode.Services.Services
{
    // WBS 10: Startup.DI binds the Brevo configuration section to these settings.
    // It uses BaseUrl for the typed HttpClient; BrevoEmailSender uses sender/API settings.
    public sealed class BrevoOptions
    {
        public const string SectionName = "Brevo";

        public string ApiKey { get; set; }

        public string BaseUrl { get; set; } = "https://api.brevo.com";

        public string SenderEmail { get; set; }

        public string SenderName { get; set; } = "Gearantee";
    }
}
