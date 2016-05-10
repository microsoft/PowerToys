using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wox.Plugin;

namespace Wox.Infrastructure.Http
{
    public static class Http
    {
        public static WebProxy WebProxy(IHttpProxy proxy)
        {
            if (proxy != null && proxy.Enabled && !string.IsNullOrEmpty(proxy.Server))
            {
                if (string.IsNullOrEmpty(proxy.UserName) || string.IsNullOrEmpty(proxy.Password))
                {
                    var webProxy = new WebProxy(proxy.Server, proxy.Port);
                    return webProxy;
                }
                else
                {
                    var webProxy = new WebProxy(proxy.Server, proxy.Port)
                    {
                        Credentials = new NetworkCredential(proxy.UserName, proxy.Password)
                    };
                    return webProxy;
                }
            }
            else
            {
                return null;
            }
        }

        /// <exception cref="WebException">Can't download file </exception>
        public static void Download([NotNull] string url, [NotNull] string filePath, IHttpProxy proxy)
        {
            var client = new WebClient { Proxy = WebProxy(proxy) };
            client.DownloadFile(url, filePath);
        }

        /// <exception cref="WebException">Can't get response from http get </exception>
        public static async Task<string> Get([NotNull] string url, IHttpProxy proxy, string encoding = "UTF-8")
        {

            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Method = "GET";
            request.Timeout = 10 * 1000;
            request.Proxy = WebProxy(proxy);
            request.UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";
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