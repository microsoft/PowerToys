using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
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
        private static readonly Internationalization Translater = InternationalizationManager.Instance;

        public static async Task UpdateApp()
        {
            UpdateManager m;
            UpdateInfo u;

            try
            {
                m = await GitHubUpdateManager(Constant.Repository);
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                var checkUpdatesFailed = Translater.GetTranslation("checkUpdatesFailed");
                Log.Exception($"|Updater.UpdateApp|{checkUpdatesFailed}", e);
                MessageBox.Show(checkUpdatesFailed);
                return;
            }

            try
            {
                // UpdateApp CheckForUpdate will return value only if the app is squirrel installed
                u = await m.CheckForUpdate().NonNull();
            }
            catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
            {
                var checkUpdatesFailed = Translater.GetTranslation("checkUpdatesFailed");
                Log.Exception($"|Updater.UpdateApp|{checkUpdatesFailed}", e);
                MessageBox.Show(checkUpdatesFailed);
                m.Dispose();
                return;
            }

            var fr = u.FutureReleaseEntry;
            var cr = u.CurrentlyInstalledVersion;
            Log.Info($"|Updater.UpdateApp|Future Release <{fr.Formatted()}>");
            if (fr.Version > cr.Version)
            {
                try
                {
                    await m.DownloadReleases(u.ReleasesToApply);
                }
                catch (Exception e) when (e is HttpRequestException || e is WebException || e is SocketException)
                {
                    var downloadUpdatesFailed = Translater.GetTranslation("downloadUpdatesFailed");
                    Log.Exception($"|Updater.UpdateApp|{downloadUpdatesFailed}", e);
                    MessageBox.Show(downloadUpdatesFailed);
                    m.Dispose();
                    return;
                }

                await m.ApplyReleases(u);
                await m.CreateUninstallerRegistryEntry();
                m.Dispose();

                var newVersionTips = Translater.GetTranslation("newVersionTips");
                newVersionTips = string.Format(newVersionTips, fr.Version);
                MessageBox.Show(newVersionTips);
                Log.Info($"|Updater.UpdateApp|Update succeed:{newVersionTips}");
            }
        }

        [UsedImplicitly]
        private class GithubRelease
        {
            [JsonProperty("prerelease")]
            public bool Prerelease { get; [UsedImplicitly] set; }

            [JsonProperty("published_at")]
            public DateTime PublishedAt { get; [UsedImplicitly] set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; [UsedImplicitly] set; }
        }

        /// https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.Factory.cs
        private static async Task<UpdateManager> GitHubUpdateManager(string repository)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/repos{uri.AbsolutePath}/releases";

            var json = await Http.Get(api);

            var releases = JsonConvert.DeserializeObject<List<GithubRelease>>(json);
            var latest = releases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).First();
            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var client = new WebClient { Proxy = Http.WebProxy() };
            var downloader = new FileDownloader(client);

            var manager = new UpdateManager(latestUrl, urlDownloader: downloader);

            return manager;
        }
    }
}