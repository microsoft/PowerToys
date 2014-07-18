using System;
using System.Net;


namespace Wox.Plugin.PluginManagement
{
    public class HttpRequest
    {
        private static readonly string DefaultUserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
     
        public static HttpWebResponse CreateGetHttpResponse(string url,IHttpProxy proxy)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException("url");
            }
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            if (proxy != null && proxy.Enabled && !string.IsNullOrEmpty(proxy.Server))
            {
                if (string.IsNullOrEmpty(proxy.UserName) || string.IsNullOrEmpty(proxy.Password))
                {
                    request.Proxy = new WebProxy(proxy.Server, proxy.Port);
                }
                else
                {
                    request.Proxy = new WebProxy(proxy.Server, proxy.Port);
                    request.Proxy.Credentials = new NetworkCredential(proxy.UserName, proxy.Password);
                }
            }
            request.Method = "GET";
            request.UserAgent = DefaultUserAgent;
            return request.GetResponse() as HttpWebResponse;
        }
    }
}
