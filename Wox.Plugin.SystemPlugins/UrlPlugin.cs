using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Wox.Plugin.SystemPlugins
{
    public class UrlPlugin : BaseSystemPlugin
    {
        protected override List<Result> QueryInternal(Query query)
        {
            var raw = query.RawQuery;
            Uri uri;
            if (Uri.TryCreate(raw, UriKind.Absolute, out uri))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = raw,
                        SubTitle = "Open the typed URL...",
                        IcoPath = "Images/url1.png",
                        Score = 8,
                        Action = _ =>
                        {
                            Process.Start(raw);
                            return true;
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
        public override string IcoPath { get { return "Images/url2.png"; } }

        protected override void InitInternal(PluginInitContext context)
        {
        }
    }
}