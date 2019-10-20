using System.Configuration;

namespace JsonToCsvMapper.Demo
{
    class Config
    {
        public static class Storage
        {
            public static string FilesPath
            {
                get { return ConfigurationManager.AppSettings["FilesPath"]; }
            }

            public static string LogFilesPath
            {
                get { return ConfigurationManager.AppSettings["LogFilesPath"]; }
            }
        }

        public static class Api
        {
            public static string ApiUrl
            {
                get { return ConfigurationManager.AppSettings["ApiUrl"]; }
            }
            public static int RetryCount
            {
                get { return int.Parse(ConfigurationManager.AppSettings["RetryCount"]); }
            }

            public static string AuthorizationKey
            {
                get { return ConfigurationManager.AppSettings["AuthorizationKey"]; }
            }
        }

        public static class Settings
        {
            public static string EmailOnFail
            {
                get { return ConfigurationManager.AppSettings["EmailOnFail"]; }
            }

            public static bool LogMissingProperties
            {
                get { return bool.Parse(ConfigurationManager.AppSettings["LogMissingProperties"] ?? "false"); }
            }

            public static string SMTPServer
            {
                get { return ConfigurationManager.AppSettings["SMTPServer"]; }
            }
        }
    }
}
