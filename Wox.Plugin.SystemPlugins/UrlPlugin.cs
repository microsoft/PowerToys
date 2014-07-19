using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows;

namespace Wox.Plugin.SystemPlugins
{
    public class UrlPlugin : BaseSystemPlugin
    {
        const string pattern = @"^(http|https|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
        Regex reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override List<Result> QueryInternal(Query query)
        {
            var raw = query.RawQuery;
            if (reg.IsMatch(raw))
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