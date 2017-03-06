using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                // UpdateApp() will return value only if the app is squirrel installed
                using (var m = await UpdateManager.GitHubUpdateManager(url, urlDownloader: d))
                {
                    var e = await m.CheckForUpdate();
                    var fe = e.FutureReleaseEntry;
                    var ce = e.CurrentlyInstalledVersion;
                    if (fe.Version > ce.Version)
                    {
                        var t = NewVersinoTips(fe.Version.ToString());
                        MessageBox.Show(t);

                        await m.DownloadReleases(e.ReleasesToApply);
                        await m.ApplyReleases(e);
                        await m.CreateUninstallerRegistryEntry();

                        Log.Info($"|Updater.UpdateApp|TEST <{e.Formatted()}>");
                        Log.Info($"|Updater.UpdateApp|TEST <{fe.Formatted()}>");
                        Log.Info($"|Updater.UpdateApp|TEST <{ce.Formatted()}>");
                        Log.Info($"|Updater.UpdateApp|TEST <{e.ReleasesToApply.Formatted()}>");
                        Log.Info($"|Updater.UpdateApp|TEST <{t.Formatted()}>");
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


        public static async Task<string> NewVersion()
        {
            const string githubAPI = @"https://api.github.com/repos/wox-launcher/wox/releases/latest";

            string response;
            try
            {
                response = await Http.Get(githubAPI);
            }
            catch (WebException e)
            {
                Log.Exception("|Updater.NewVersion|Can't connect to github api to check new version", e);
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(response))
            {
                JContainer json;
                try
                {
                    json = (JContainer)JsonConvert.DeserializeObject(response);
                }
                catch (JsonSerializationException e)
                {
                    Log.Exception("|Updater.NewVersion|can't parse response", e);
                    return string.Empty;
                }
                var version = json?["tag_name"]?.ToString();
                if (!string.IsNullOrEmpty(version))
                {
                    return version;
                }
                else
                {
                    Log.Warn("|Updater.NewVersion|Can't find tag_name from Github API response");
                    return string.Empty;
                }
            }
            else
            {
                Log.Warn("|Updater.NewVersion|Can't get response from Github API");
                return string.Empty;
            }
        }

        public static int NumericVersion(string version)
        {
            var newVersion = version.Replace("v", ".").Replace(".", "").Replace("*", "");
            return int.Parse(newVersion);
        }

        public static string NewVersinoTips(string version)
        {
            var translater = InternationalizationManager.Instance;
            var tips = string.Format(translater.GetTranslation("newVersionTips"), version);
            return tips;
        }

    }
}
