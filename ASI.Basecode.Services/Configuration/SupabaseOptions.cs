namespace ASI.Basecode.Services.Configuration
{
    public sealed class SupabaseOptions
    {
        public const string SectionName = "Supabase";

        /// <summary>Project URL, e.g. https://xxxx.supabase.co.</summary>
        public string Url { get; set; }

        /// <summary>Public storage bucket that holds equipment images.</summary>
        public string Bucket { get; set; } = "equipment-images";

        /// <summary>service_role key. Secret: server only, never committed or logged.</summary>
        public string ServiceRoleKey { get; set; }

        /// <summary>Maximum accepted image size in bytes.</summary>
        public long MaxFileBytes { get; set; } = 2 * 1024 * 1024;

        /// <summary>True when a Supabase project is configured and uploads are enabled.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ServiceRoleKey);
    }
}
