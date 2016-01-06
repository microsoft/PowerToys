using System;
using System.IO;
using System.Net;
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
            FeedUrl = feedUrl;
            Proxy = proxy;
        }

        private void TryResolvingHost()
        {
            Uri uri = new Uri(FeedUrl);
            try
            {
                Dns.GetHostEntry(uri.Host);
            }
            catch (Exception ex)
            {
                throw new WebException(string.Format("Failed to resolve {0}. Check your connectivity.", uri.Host), WebExceptionStatus.ConnectFailure);
            }
        }

        public string GetUpdatesFeed()
        {
            TryResolvingHost();
            string str = string.Empty;
            WebRequest webRequest = WebRequest.Create(FeedUrl);
            webRequest.Method = "GET";
            webRequest.Proxy = Proxy;
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
            fileDownloader.Proxy = Proxy;
            if (string.IsNullOrEmpty(tempLocation) || !Directory.Exists(Path.GetDirectoryName(tempLocation)))
                tempLocation = Path.GetTempFileName();
            return fileDownloader.DownloadToFile(tempLocation, onProgress);
        }
    }
}
