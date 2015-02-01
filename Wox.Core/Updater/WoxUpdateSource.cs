using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using NAppUpdate.Framework.Utils;

namespace Wox.Core.Updater
{
    internal class WoxUpdateSource : IUpdateSource
    {
        public IWebProxy Proxy { get; set; }

        public string FeedUrl { get; set; }

        public WoxUpdateSource(string feedUrl,IWebProxy proxy)
        {
            this.FeedUrl = feedUrl;
            this.Proxy = proxy;
        }

        private void TryResolvingHost()
        {
            Uri uri = new Uri(this.FeedUrl);
            try
            {
                Dns.GetHostEntry(uri.Host);
            }
            catch (System.Exception ex)
            {
                throw new WebException(string.Format("Failed to resolve {0}. Check your connectivity.", (object)uri.Host), WebExceptionStatus.ConnectFailure);
            }
        }

        public string GetUpdatesFeed()
        {
            this.TryResolvingHost();
            string str = string.Empty;
            WebRequest webRequest = WebRequest.Create(this.FeedUrl);
            webRequest.Method = "GET";
            webRequest.Proxy = this.Proxy;
            using (WebResponse response = webRequest.GetResponse())
            {
                Stream responseStream = response.GetResponseStream();
                if (responseStream != null)
                {
                    using (StreamReader streamReader = new StreamReader(responseStream, true))
                        str = streamReader.ReadToEnd();
                }
            }
            return str;
        }

        public bool GetData(string url, string baseUrl, Action<UpdateProgressInfo> onProgress, ref string tempLocation)
        {
            if (!string.IsNullOrEmpty(baseUrl) && !baseUrl.EndsWith("/"))
                baseUrl += "/";
            FileDownloader fileDownloader = !Uri.IsWellFormedUriString(url, UriKind.Absolute) ? (!Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute) ? (string.IsNullOrEmpty(baseUrl) ? new FileDownloader(url) : new FileDownloader(new Uri(new Uri(baseUrl), url))) : new FileDownloader(new Uri(new Uri(baseUrl, UriKind.Absolute), url))) : new FileDownloader(url);
            fileDownloader.Proxy = this.Proxy;
            if (string.IsNullOrEmpty(tempLocation) || !Directory.Exists(Path.GetDirectoryName(tempLocation)))
                tempLocation = Path.GetTempFileName();
            return fileDownloader.DownloadToFile(tempLocation, onProgress);
        }
    }
}
