using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Extensions.Configuration
{
    public static class ConfigurationExtensions
    {
        public static string GetSetupRootDirectoryPath(
            this IConfiguration configuration)
        {
            return configuration.GetSection("Common")
                .GetValue<string>("SetupRoot");
        }

        public static IConfigurationSection GetLoggingSection(
            this IConfiguration configuration)
        {
            return configuration.GetSection("Logging");
        }

        public static LogLevel GetLoggingLogLevel(
            this IConfiguration configuration,
            string name = "Default")
        {
            return configuration.GetSection("Logging")
                .GetValue<LogLevel>($"LogLevel:{name}");
        }

        public static string GetLogFileSize(
            this IConfiguration configuration)
        {
            return configuration.GetSection("Common")
                .GetValue<string>("LogFileSize");
        }
    }
}
