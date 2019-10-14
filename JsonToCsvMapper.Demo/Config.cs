using System.Configuration;

namespace JsonToCsvMapper.Demo
{
    class Config
    {
        public static class Storage
        {
            public static string TempFilesPath
            {
                get { return ConfigurationManager.AppSettings["TempFilesPath"]; }
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

        public static class Ftp
        {
            public static class Data
            {
                public static string PathComplete
                {
                    get { return ConfigurationManager.AppSettings["FTPDataPathComplete"]; }
                }

                public static string FileNameComplete
                {
                    get { return ConfigurationManager.AppSettings["FTPDataFileNameComplete"]; }
                }
            }

            public static class Media
            {
                public static string PathComplete
                {
                    get { return ConfigurationManager.AppSettings["FTPMediaPathComplete"]; }
                }

                public static string FileNameComplete
                {
                    get { return ConfigurationManager.AppSettings["FTPMediaFileNameComplete"]; }
                }
            }

            public static class CatalogFile
            {
                public static string Path
                {
                    get { return ConfigurationManager.AppSettings["FTPCatalogFilePath"]; }
                }
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
