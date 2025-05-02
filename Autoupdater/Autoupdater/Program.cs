using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autoupdater
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            args = new string[] { "checkversion" };
            args = new string[] { "updatenow" };
#endif
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: autoupdater.exe [checkversion|updatenow]");
                return;
            }

            var task = RunAsync(args[0].ToLower());
            task.Wait();
        }

        static async Task RunAsync(string command)
        {
            var updater = new Updater("updates.json");

            switch (command)
            {
                case "checkversion":
                    var hasUpdate = await updater.CheckForUpdatesAsync();
                    Environment.Exit(hasUpdate ? 1 : 0);
                    break;

                case "updatenow":
                    await updater.PerformUpdateAsync();
                    break;

                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
    }
}
