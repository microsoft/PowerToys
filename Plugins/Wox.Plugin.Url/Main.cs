using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.Url
{
    public class Main : ISettingProvider,IPlugin, IPluginI18n, ISavable
    {
        //based on https://gist.github.com/dperini/729294
        private const string urlPattern = "^" +
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
        private PluginInitContext context;
        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            var raw = query.Search;
            if (IsURL(raw))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = raw,
                        SubTitle = string.Format(context.API.GetTranslation("wox_plugin_url_open_url"),raw),
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
                                if (_settings.BrowserPath.Length == 0)
                                {
                                    Process.Start(raw);
                                }
                                else
                                {
                                    Process.Start(_settings.BrowserPath,raw);
                                }

                                return true;
                            }
                            catch(Exception ex)
                            {
                                context.API.ShowMsg(string.Format(context.API.GetTranslation("wox_plugin_url_canot_open_url"), raw));
                                return false;
                            }
                        }
                    }
                };
            }
            return new List<Result>(0);
        }


        public Control CreateSettingPanel()
        {
            return new SettingsControl(context.API,_settings);
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

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_url_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_url_plugin_description");
        }
    }
}