using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Autoupdater
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autoupdater.log");

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch
            {

            }         
        }

        public static void Reset()
        {
            try
            {
                if (File.Exists(LogPath))
                    File.Delete(LogPath);

                Write("Log resat");
            }
            catch
            {
            }
        }
    }
}
