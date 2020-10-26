// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Http
{
    public static class Http
    {
        private const string UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";

        static Http()
        {
            // need to be added so it would work on a win10 machine
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls
                                                    | SecurityProtocolType.Tls11
                                                    | SecurityProtocolType.Tls12;
        }

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
                        Credentials = new NetworkCredential(Proxy.UserName, Proxy.Password),
                    };
                    return webProxy;
                }
            }
            else
            {
                return WebRequest.GetSystemWebProxy();
            }
        }

        public static void Download([NotNull] Uri url, [NotNull] string filePath)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var client = new WebClient { Proxy = WebProxy() };
            client.Headers.Add("user-agent", UserAgent);
            client.DownloadFile(url.AbsoluteUri, filePath);
            client.Dispose();
        }

        public static async Task<string> Get([NotNull] Uri url, string encoding = "UTF-8")
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            Log.Debug($"Url <{url.AbsoluteUri}>", MethodBase.GetCurrentMethod().DeclaringType);

            var request = WebRequest.CreateHttp(url.AbsoluteUri);
            request.Method = "GET";
            request.Timeout = 1000;
            request.Proxy = WebProxy();
            request.UserAgent = UserAgent;
            var response = await request.GetResponseAsync() as HttpWebResponse;
            response = response.NonNull();
            var stream = response.GetResponseStream().NonNull();

            using (var reader = new StreamReader(stream, Encoding.GetEncoding(encoding)))
            {
                var content = await reader.ReadToEndAsync();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return content;
                }
                else
                {
                    throw new HttpRequestException($"Error code <{response.StatusCode}> with content <{content}> returned from <{url.AbsoluteUri}>");
                }
            }
        }
    }
}
