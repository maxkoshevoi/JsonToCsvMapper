using System.Configuration;

namespace JsonToCsvMapper.Demo
{
    class Config
    {
        public static class Storage
        {
            public static string FilesPath => ConfigurationManager.AppSettings[nameof(FilesPath)];

            public static string LogFilesPath => ConfigurationManager.AppSettings[nameof(LogFilesPath)];
        }

        public static class Api
        {
            public static string ApiUrl => ConfigurationManager.AppSettings[nameof(ApiUrl)];

            public static int RetryCount => int.Parse(ConfigurationManager.AppSettings[nameof(RetryCount)]);

            public static string AuthorizationKey => ConfigurationManager.AppSettings[nameof(AuthorizationKey)];
        }

        public static class Settings
        {
            public static string EmailOnFail => ConfigurationManager.AppSettings[nameof(EmailOnFail)];

            public static bool LogMissingProperties => bool.Parse(ConfigurationManager.AppSettings[nameof(LogMissingProperties)] ?? bool.FalseString);

            public static string SMTPServer => ConfigurationManager.AppSettings[nameof(SMTPServer)];
        }
    }
}
