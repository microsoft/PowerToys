using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin.SystemPlugins.SuggestionSources;

namespace Wox.Plugin.SystemPlugins
{
    public class WebSearchPlugin : BaseSystemPlugin, ISettingProvider
    {
        private PluginInitContext context;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();

            WebSearch webSearch =
                UserSettingStorage.Instance.WebSearches.FirstOrDefault(o => o.ActionWord == query.ActionName && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.ActionParameters.Count > 0 ? query.GetAllRemainingParameter() : "";
                string title = keyword;
                string subtitle = "Search " + webSearch.Title;
                if (string.IsNullOrEmpty(keyword))
                {
                    title = subtitle;
                    subtitle = null;
                }
                context.API.PushResults(query, context.CurrentPluginMetadata, new List<Result>()
                {
                    new Result()
                    {
                        Title = title,
                        SubTitle = subtitle,
                        Score = 6,
                        IcoPath = webSearch.IconPath,
                        Action = (c) =>
                        {
                            Process.Start(webSearch.Url.Replace("{q}", keyword));
                            return true;
                        }
                    }
                });

                if (UserSettingStorage.Instance.EnableWebSearchSuggestion && !string.IsNullOrEmpty(keyword))
                {
                    ISuggestionSource sugg = SuggestionSourceFactory.GetSuggestionSource(
                            UserSettingStorage.Instance.WebSearchSuggestionSource);
                    if (sugg != null)
                    {
                        var result = sugg.GetSuggestions(keyword);
                        if (result != null)
                        {
                            context.API.PushResults(query, context.CurrentPluginMetadata,
                                result.Select(o => new Result()
                                {
                                    Title = o,
                                    SubTitle = subtitle,
                                    Score = 5,
                                    IcoPath = webSearch.IconPath,
                                    Action = (c) =>
                                    {
                                        Process.Start(webSearch.Url.Replace("{q}", o));
                                        return true;
                                    }
                                }).ToList());
                        }
                    }
                }
            }

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;

            if (UserSettingStorage.Instance.WebSearches == null)
                UserSettingStorage.Instance.WebSearches = UserSettingStorage.Instance.LoadDefaultWebSearches();
        }

        public override string ID
        {
            get { return "565B73353DBF4806919830B9202EE3BF"; }
        }

        public override string Name
        {
            get { return "Web Searches"; }
        }

        public override string IcoPath
        {
            get { return @"Images\web_search.png"; }
        }

        public override string Description
        {
            get { return "Provide the web search ability."; }
        }

        #region ISettingProvider Members

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new WebSearchesSetting();
        }

        #endregion
    }
}
