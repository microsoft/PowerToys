using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Infrastructure.Http
{
    public static class HttpRequest
    {
        private static WebProxy GetWebProxy(IHttpProxy proxy)
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

        public static async Task<string> Get([NotNull] string url, IHttpProxy proxy, string encoding = "UTF-8")
        {

            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Method = "GET";
            request.Timeout = 10 * 1000;
            request.Proxy = GetWebProxy(proxy);
            request.UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";
            HttpWebResponse response;
            try
            {
                response = await request.GetResponseAsync() as HttpWebResponse;
            }
            catch (WebException e)
            {
                Log.Error(e);
                return string.Empty;
            }
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