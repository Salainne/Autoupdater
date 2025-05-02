using System;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Autoupdater.Integrations
{
    public class GithubUpdateSource: IUpdateSource
    {
        private readonly string _repo;
        private readonly Regex _assetMatch;

        public GithubUpdateSource(string repo, string assetPattern)
        {
            _repo = repo;
            _assetMatch = new Regex(assetPattern, RegexOptions.IgnoreCase);
        }

        public async Task<UpdateInfo> CheckForUpdateAsync(string currentVersion)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("autoupdater");

                var response = await client.GetAsync($"https://api.github.com/repos/{_repo}/releases/latest");
                if(!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var tag = json["tag_name"]?.ToString();
                if(tag == null || tag == currentVersion)
                {
                    return null;
                }

                var assets = json["assets"] as JArray;
                if (assets == null)
                {
                    return null;
                }

                foreach(var asset in assets)
                {
                    var name = asset["name"]?.ToString();
                    if(!string.IsNullOrEmpty(name) && _assetMatch.IsMatch(name))
                    {
                        var downloadUrl = asset["browser_download_url"]?.ToString();
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            return new UpdateInfo
                            {
                                Version = tag,
                                FileName = name,
                                DownloadUrl = downloadUrl
                            };
                        }
                    }
                }
                return null;
            }
        }

        public async Task DownloadLatestAsync(string destinationPath)
        {
            var update = await CheckForUpdateAsync("");
            if (update == null)
            {
                return;
            }

            using (var client = new HttpClient())
            {
                var data = await client.GetByteArrayAsync(update.DownloadUrl);
                var filePath = Path.Combine(destinationPath, update.FileName);
                File.WriteAllBytes(filePath, data);
            }
        }
    }
}
