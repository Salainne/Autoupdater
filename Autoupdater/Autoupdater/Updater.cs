using Autoupdater.Integrations;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autoupdater
{
    public class Updater
    {
        private readonly string _configPath;
        private JObject _config;

        public Updater(string configPath)
        {
            _configPath = configPath;

            if (!File.Exists(_configPath))
            {
                Logger.Write("ERROR: Could not find updates.json at " + _configPath);
                throw new FileNotFoundException("updates.json is missing.", _configPath);
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                _config = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                Logger.Write("ERROR: Failed to read or parse updates.json.\n" + ex);
                throw;
            }
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            Logger.Reset();
            var currentVersion = _config["version"]?.ToString();
            var sources = GetSources();

            Logger.Write("Checking for updates...");
            foreach (var src in sources)
            {
                var update = await src.CheckForUpdateAsync(currentVersion);
                if(update != null)
                {
                    Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update, Newtonsoft.Json.Formatting.Indented));
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "availableupdate.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(update, Newtonsoft.Json.Formatting.Indented)
                    );
                    Logger.Write($"Update found: {update.Version}");
                    return true;
                }
                else
                {
                    Logger.Write("No update found.");
                }
            }
            return false;
        }

        public async Task PerformUpdateAsync()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempPath = Path.Combine(baseDir, "updatetemp");
            var extractPath = Path.Combine(tempPath, "extracted");
            var parentDir = Directory.GetParent(Directory.GetParent(Directory.GetParent(extractPath).FullName).FullName).FullName;

            Logger.Write("Starting update process...");

            // Clean up temp folder
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);

            Directory.CreateDirectory(tempPath);

            var sources = GetSources();

            foreach (var src in sources)
            {
                await src.DownloadLatestAsync(tempPath);

                var zip = Directory.GetFiles(tempPath, "*.zip").FirstOrDefault();
                if (zip != null)
                {
                    // Ensure extraction folder is clean
                    if (Directory.Exists(extractPath))
                    {
                        if (Directory.EnumerateFileSystemEntries(extractPath).Any())
                        {
                            Logger.Write("Previous extraction folder not empty – cleaning up.");
                            Directory.Delete(extractPath, true);
                            Directory.CreateDirectory(extractPath);
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(extractPath);
                    }

                    Logger.Write("Extracting zip file...");
                    ZipFile.ExtractToDirectory(zip, extractPath);

                    // Find the folder containing the .exe (first one found)
                    string newRoot = extractPath;
                    if(Directory.GetFiles(extractPath, "*.exe", SearchOption.TopDirectoryOnly).Length <= 0)
                    {
                        foreach (var dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                        {
                            var exeFiles = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
                            if (exeFiles.Length > 0)
                            {
                                newRoot = dir;
                                break;
                            }
                        }
                    }
                    
                    if (newRoot == null)
                    {
                        Logger.Write("No .exe file found in the archive – update aborted.");
                        return;
                    }

                    Logger.Write("Copying files from: " + newRoot);

                    var exefileToStartAfterUpdate = "";

                    foreach (var file in Directory.GetFiles(newRoot, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(newRoot.Length + 1);

                        // Skip any files inside the autoupdater folder
                        if (relativePath.StartsWith("autoupdater\\", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Write("Skipped file in autoupdater folder: " + relativePath);
                            continue;
                        }

                        var destPath = Path.Combine(parentDir, relativePath);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);


                        File.Copy(file, destPath, true);
                        if(string.IsNullOrEmpty(exefileToStartAfterUpdate) && (new FileInfo(destPath)).Extension == ".exe")
                        {
                            exefileToStartAfterUpdate = destPath;
                        }
                        Logger.Write("Copied: " + relativePath);
                    }

                    UpdateLocalVersionFromAvailableInfo();

                    // Clean up temp folder
                    Directory.Delete(tempPath, true);
                    Logger.Write("Update completed successfully.");

                    if(string.IsNullOrEmpty(exefileToStartAfterUpdate) == false)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exefileToStartAfterUpdate,
                            UseShellExecute = false,
                            CreateNoWindow = false,
                            WorkingDirectory = Path.GetDirectoryName(exefileToStartAfterUpdate)
                        });
                    }

                    break;
                }
                else
                {
                    Logger.Write("No zip file found – no update was performed.");
                }
            }
        }

        private void UpdateLocalVersionFromAvailableInfo()
        {
            try
            {
                var infoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "availableupdate.json");
                if (!File.Exists(infoPath))
                {
                    Logger.Write("No availableupdate.json found — skipping version update.");
                    return;
                }

                var json = File.ReadAllText(infoPath);
                var update = Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateInfo>(json);
                if (update == null || string.IsNullOrEmpty(update.Version))
                {
                    Logger.Write("availableupdate.json is invalid — cannot update local version.");
                    return;
                }

                _config["version"] = update.Version;
                File.WriteAllText(_configPath, _config.ToString());
                Logger.Write("updates.json version updated to: " + update.Version);

                File.Delete(infoPath);
                Logger.Write("Deleted availableupdate.json.");
            }
            catch (Exception ex)
            {
                Logger.Write("WARNING: Failed to update version in updates.json: " + ex.Message);
            }
        }

        private List<IUpdateSource> GetSources()
        {
            var list = new List<IUpdateSource>();
            var sources = _config["sources"] as JArray;
            if(sources == null)
            {
                return list;
            }

            foreach(var source in sources)
            {
                var type = source["type"]?.ToString();
                if(type == "github")
                {
                    list.Add(new GithubUpdateSource(source["repo"]?.ToString(), source["assetMatch"]?.ToString()));
                }

                // more sourcetypes later..
            }

            return list;
        }
    }
}
