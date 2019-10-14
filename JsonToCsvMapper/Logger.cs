using System;
using System.IO;
using System.Text;

namespace JsonToCsvMapper
{
    public class Logger
    {
        private readonly string DirPath;
        private string cache;

        public Logger(string dirPath)
        {
            DirPath = dirPath;
            cache = string.Empty;
        }

        public void WriteLine(string format, object arg0) => WriteLine(string.Format(format, arg0));

        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

        public void WriteLine(string text)
        {
            text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss - ") + text + Environment.NewLine;
            AppendFile(text);
            cache += text;
        }

        public void WriteError(Exception ex, bool onlyMessage = false)
        {
            if (onlyMessage)
            {
                WriteLine(ex.Message);
            }
            else
            {
                WriteLine(ex.ToString());
            }
        }
        
        private void AppendFile(string text)
        {
            File.AppendAllText(GetFileName(), text, Encoding.UTF8);
        }

        private string GetFileName()
        {
            string name = Path.Combine(DirPath, DateTime.Now.ToString("MM.yyyy") + ".txt");
            if (!File.Exists(name))
            {
                File.Create(name).Close();
            }
            return name;
        }

        public string GetAll()
        {
            return cache;
        }
    }
}
