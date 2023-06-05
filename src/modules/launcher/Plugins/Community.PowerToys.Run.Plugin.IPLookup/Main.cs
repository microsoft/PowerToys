// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using ManagedCommon;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.IPLookup
{
    public class Main : IPlugin
    {
        private PluginInitContext? _context;
        private string? _iconPath;

        public string Name => Properties.Resources.iplookup_plugin_name;

        public string Description => Properties.Resources.iplookup_plugin_description;

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/IPLookup_icon_light.png";
            }
            else
            {
                _iconPath = "Images/IPLookup_icon_dark.png";
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (string.IsNullOrEmpty(query.Search))
            {
                // Get IPv4
                var client = new HttpClient();
                var response = client.GetAsync("http://ipv4.icanhazip.com");
                var ipv4Result = response.Result.Content.ReadAsStringAsync().Result;

                Result result = new Result()
                {
                    Title = ipv4Result,
                    SubTitle = Properties.Resources.iplookup_plugin_ipv4_copy,
                    IcoPath = _iconPath,
                    Action = _ => CopyToClipboard(ipv4Result),
                };

                results.Add(result);

                // Get IPv6 address. This is in a try catch because not everyone has IPv6.
                try
                {
                    var ipv6Response = client.GetAsync("http://ipv6.icanhazip.com");
                    var ipv6Result = ipv6Response.Result.Content.ReadAsStringAsync().Result;
                    Result resultIpv6 = new Result()
                    {
                        Title = ipv6Result,
                        SubTitle = Properties.Resources.iplookup_plugin_ipv6_copy,
                        IcoPath = _iconPath,
                        Action = _ => CopyToClipboard(ipv6Result),
                    };
                    results.Add(resultIpv6);
                }
                catch
                {
                    Result errorResult = new Result()
                    {
                        Title = Properties.Resources.iplookup_plugin_ipv6_error_title,
                        SubTitle = Properties.Resources.iplookup_plugin_ipv6_error_subtitle,
                        IcoPath = _iconPath,
                    };

                    results.Add(errorResult);
                }
            }

            return results;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public bool CopyToClipboard(string? text)
        {
            Clipboard.Clear();
            Clipboard.SetText(text);
            return true;
        }
    }
}
