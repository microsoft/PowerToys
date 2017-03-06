using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Squirrel;
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
                const string url = Infrastructure.Constant.Github;
                // todo 5/9 the return value of UpdateApp() is NULL, fucking useless!
                using (var m = await UpdateManager.GitHubUpdateManager(url, urlDownloader: d))
                {
                    await m.UpdateApp();
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
    }
}
