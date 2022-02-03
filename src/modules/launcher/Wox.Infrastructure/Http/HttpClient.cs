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
    public static class HttpClient
    {
        private const string UserAgent = @"Mozilla/5.0 (Trident/7.0; rv:11.0) like Gecko";

        public static HttpProxy Proxy { get; set; }

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

#pragma warning disable SYSLIB0014 // Type or member is obsolete

            // TODO: Verify if it's dead code or replace with HttpClient
            var client = new WebClient { Proxy = WebProxy() };
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            client.Headers.Add("user-agent", UserAgent);
            client.DownloadFile(url.AbsoluteUri, filePath);
            client.Dispose();
        }
    }
}
