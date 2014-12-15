using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows;

namespace Wox.Plugin.SystemPlugins
{
    public class UrlPlugin : BaseSystemPlugin
    {
        //based on https://gist.github.com/dperini/729294
        private const string urlPattern ="^" +
            // protocol identifier
            "(?:(?:https?|ftp)://|)" +
            // user:pass authentication
            "(?:\\S+(?::\\S*)?@)?" +
            "(?:" +
            // IP address exclusion
            // private & local networks
            "(?!(?:10|127)(?:\\.\\d{1,3}){3})" +
            "(?!(?:169\\.254|192\\.168)(?:\\.\\d{1,3}){2})" +
            "(?!172\\.(?:1[6-9]|2\\d|3[0-1])(?:\\.\\d{1,3}){2})" +
            // IP address dotted notation octets
            // excludes loopback network 0.0.0.0
            // excludes reserved space >= 224.0.0.0
            // excludes network & broacast addresses
            // (first & last IP address of each class)
            "(?:[1-9]\\d?|1\\d\\d|2[01]\\d|22[0-3])" +
            "(?:\\.(?:1?\\d{1,2}|2[0-4]\\d|25[0-5])){2}" +
            "(?:\\.(?:[1-9]\\d?|1\\d\\d|2[0-4]\\d|25[0-4]))" +
            "|" +
            // host name
            "(?:(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)" +
            // domain name
            "(?:\\.(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)*" +
            // TLD identifier
            "(?:\\.(?:[a-z\\u00a1-\\uffff]{2,}))" +
            ")" +
            // port number
            "(?::\\d{2,5})?" +
            // resource path
            "(?:/\\S*)?" +
            "$";
        Regex reg = new Regex(urlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override List<Result> QueryInternal(Query query)
        {
            var raw = query.RawQuery;
            if (IsURL(raw))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = raw,
                        SubTitle = "Open " +  raw,
                        IcoPath = "Images/url.png",
                        Score = 8,
                        Action = _ =>
                        {
                            if (!raw.ToLower().StartsWith("http"))
                            {
                                raw = "http://" + raw;
                            }
                            try
                            {
                                Process.Start(raw);
                                return true;
                            }
                            catch(Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Could not open " +  raw);
                                return false;
                            }
                        }
                    }
                };
            }
            return new List<Result>(0);
        }

        public bool IsURL(string raw)
        {
            raw = raw.ToLower();

            if (reg.Match(raw).Value == raw) return true;

            if (raw == "localhost" || raw.StartsWith("localhost:") ||
                raw == "http://localhost" || raw.StartsWith("http://localhost:") ||
                raw == "https://localhost" || raw.StartsWith("https://localhost:")
                )
            {
                return true;
            }

            return false;
        }

        public override string ID
        {
            get { return "0308FD86DE0A4DEE8D62B9B535370992"; }
        }

        public override string Name { get { return "URL handler"; } }
        public override string Description { get { return "Provide Opening the typed URL from Wox."; } }
        public override string IcoPath { get { return "Images/url.png"; } }

        protected override void InitInternal(PluginInitContext context)
        {
        }
    }
}