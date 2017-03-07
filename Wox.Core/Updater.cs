using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
using Newtonsoft.Json;
using Wox.Core.Resource;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Core
{
    public static class Updater
    {
        public static async Task UpdateApp()
        {
            try
            {
                using (var m = await GitHubUpdateManager(Constant.Repository))
                {
                    // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                    var e = await m.CheckForUpdate().NonNull();
                    var fe = e.FutureReleaseEntry;
                    var ce = e.CurrentlyInstalledVersion;
                    if (fe.Version > ce.Version)
                    {
                        var t = NewVersinoTips(fe.Version.ToString());
                        MessageBox.Show(t);

                        await m.DownloadReleases(e.ReleasesToApply);
                        await m.ApplyReleases(e);
                        await m.CreateUninstallerRegistryEntry();

                        Log.Info($"|Updater.UpdateApp|Future Release <{fe.Formatted()}>");
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                Log.Exception("|Updater.UpdateApp|Network error", e);
            }
            catch (Exception e)
            {
                const string info = "Update.exe not found, not a Squirrel-installed app?";
                if (e.Message == info)
                {
                    Log.Error($"|Updater.UpdateApp|{info}");
                }
                else
                {
                    throw;
                }
            }
        }

        private static string NewVersinoTips(string version)
        {
            var translater = InternationalizationManager.Instance;
            var tips = string.Format(translater.GetTranslation("newVersionTips"), version);
            return tips;
        }

        class GithubRelease
        {
            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("published_at")]
            public DateTime PublishedAt { get; set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; set; }
        }

        /// https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.Factory.cs
        private static async Task<UpdateManager> GitHubUpdateManager(string repository)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/{uri.AbsolutePath}/releases";

            var json = await Http.Get(api);

            var releases = JsonConvert.DeserializeObject<List<GithubRelease>>(json);
            var latest = releases.Where(r => r.Prerelease).OrderByDescending(r => r.PublishedAt).First();
            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var client = new WebClient { Proxy = Http.WebProxy() };
            var downloader = new FileDownloader(client);

            var manager = new UpdateManager(latestUrl, urlDownloader: downloader);

            return manager;
        }
    }
}