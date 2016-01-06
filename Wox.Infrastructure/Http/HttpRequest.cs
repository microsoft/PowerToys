using System.IO;
using System.Net;
using System.Text;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Infrastructure.Http
{
    public class HttpRequest
    {
        public static string Get(string url, IHttpProxy proxy, string encoding = "UTF-8")
        {
            return Get(url, encoding, proxy);
        }

        public static WebProxy GetWebProxy(IHttpProxy proxy)
        {
            if (proxy != null && proxy.Enabled && !string.IsNullOrEmpty(proxy.Server))
            {
                if (string.IsNullOrEmpty(proxy.UserName) || string.IsNullOrEmpty(proxy.Password))
                {
                    return new WebProxy(proxy.Server, proxy.Port);
                }

                return new WebProxy(proxy.Server, proxy.Port)
                {
                    Credentials = new NetworkCredential(proxy.UserName, proxy.Password)
                };
            }

            return null;
        }

        private static string Get(string url, string encoding, IHttpProxy proxy)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.Timeout = 10 * 1000;
            request.Proxy = GetWebProxy(proxy);

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
            catch (System.Exception e)
            {
                Log.Error(e);
                return string.Empty;
            }

            return string.Empty;
        }

        public static string Post(string url, string jsonData, IHttpProxy proxy)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;

            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "text/json";
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
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(jsonData);
                streamWriter.Flush();
                streamWriter.Close();
            }

            try
            {
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                if (response != null)
                {
                    Stream stream = response.GetResponseStream();
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Error(e);
                return string.Empty;
            }

            return string.Empty;
        }
    }
}