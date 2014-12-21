using System;
using System.IO;
using System.Net;
using System.Text;
using Wox.Plugin;

namespace Wox.Infrastructure.Http
{
    public class HttpRequest
    {
        public static string Get(string url, string encoding = "UTF8")
        {
            return Get(url, encoding, HttpProxy.Instance);
        }

        private static string Get(string url, string encoding, IHttpProxy proxy)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.Timeout = 10 * 1000;
            if (proxy != null && proxy.Enabled && !string.IsNullOrEmpty(proxy.Server))
            {
                if (string.IsNullOrEmpty(proxy.UserName) || string.IsNullOrEmpty(proxy.Password))
                {
                    request.Proxy = new WebProxy(proxy.Server, proxy.Port);
                }
                else
                {
                    request.Proxy = new WebProxy(proxy.Server, proxy.Port)
                    {
                        Credentials = new NetworkCredential(proxy.UserName, proxy.Password)
                    };
                }
            }

            try
            {
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                if (response != null)
                {
                    Stream stream = response.GetResponseStream();
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(encoding)))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return string.Empty;
            }

            return string.Empty;
        }
    }
}