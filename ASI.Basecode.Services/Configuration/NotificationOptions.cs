namespace ASI.Basecode.Services.Configuration
{
    public class NotificationOptions
    {
        public const string SectionName = "Notifications";
        public bool Enabled { get; set; }
        public int DueSoonHours { get; set; } = 24;
    }
}
