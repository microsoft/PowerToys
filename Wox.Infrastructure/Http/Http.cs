using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wox.Infrastructure.UserSettings;

namespace Wox.Infrastructure.Http
{
    public static class Http
    {
        private const string UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";

        public static HttpProxy Proxy { private get; set; }
        public static IWebProxy WebProxy()
        {
            if (Proxy != null && Proxy.Enabled && !string.IsNullOrEmpty(Proxy.Server))
            {
                if (string.IsNullOrEmpty(Proxy.UserName) || string.IsNullOrEmpty(Proxy.Password))
                {
                    var webProxy = new WebProxy(Proxy.Server, Proxy.Port);
                    return webProxy;
                }
                else
                {
                    var webProxy = new WebProxy(Proxy.Server, Proxy.Port)
                    {
                        Credentials = new NetworkCredential(Proxy.UserName, Proxy.Password)
                    };
                    return webProxy;
                }
            }
            else
            {
                return WebRequest.GetSystemWebProxy();
            }
        }

        /// <exception cref="WebException">Can't download file </exception>
        public static void Download([NotNull] string url, [NotNull] string filePath)
        {
            var client = new WebClient { Proxy = WebProxy() };
            client.Headers.Add("user-agent", UserAgent);
            client.DownloadFile(url, filePath);
        }

        /// <exception cref="WebException">Can't get response from http get </exception>
        public static async Task<string> Get([NotNull] string url, string encoding = "UTF-8")
        {

            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Method = "GET";
            request.Timeout = 10 * 1000;
            request.Proxy = WebProxy();
            request.UserAgent = UserAgent;
            var response = await request.GetResponseAsync() as HttpWebResponse;
            if (response != null)
            {
                var stream = response.GetResponseStream();
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.GetEncoding(encoding)))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }
    }
}