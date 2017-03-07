using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
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

            var c = new WebClient { Proxy = Http.WebProxy() };
            var d = new FileDownloader(c);

            try
            {
                const string url = Constant.Github;
                using (var m = await UpdateManager.GitHubUpdateManager(url, urlDownloader: d))
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

    }
}
