using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wox.Infrastructure;

namespace Wox.Plugin.ControlPanel
{
    public class Main : IPlugin, IPluginI18n
    {
        private PluginInitContext context;
        private List<ControlPanelItem> controlPanelItems = new List<ControlPanelItem>();
        private string iconFolder;
        private string fileType;

        public void Init(PluginInitContext context)
        {
            this.context = context;
            controlPanelItems = ControlPanelList.Create(48);
            iconFolder = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, @"Images\ControlPanelIcons\");
            fileType = ".bmp";

            if (!Directory.Exists(iconFolder))
            {
                Directory.CreateDirectory(iconFolder);
            }


            foreach (ControlPanelItem item in controlPanelItems)
            {
                if (!File.Exists(iconFolder + item.GUID + fileType) && item.Icon != null)
                {
                    item.Icon.ToBitmap().Save(iconFolder + item.GUID + fileType);
                }
            }

            var characters = controlPanelItems.Select(i => i.LocalizedString)
                .Concat(controlPanelItems.Select(i => i.InfoTip));

            Parallel.ForEach(characters, c =>
            {
                if (!string.IsNullOrWhiteSpace(c) && Alphabet.ContainsChinese(c))
                {
                    Alphabet.PinyinComination(c);
                }
            });
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();

            foreach (var item in controlPanelItems)
            {
                item.Score = Score(item, query.Search);
                if (item.Score > 0)
                {
                    var result = new Result
                    {
                        Title = item.LocalizedString,
                        SubTitle = item.InfoTip,
                        Score = item.Score,
                        IcoPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory,
                        @"Images\\ControlPanelIcons\\" + item.GUID + fileType),
                        Action = e =>
                            {
                                try
                                {
                                    Process.Start(item.ExecutablePath);
                                }
                                catch (Exception)
                                {
                                    //Silently Fail for now.. todo
                                }
                                return true;
                            }
                    };
                    results.Add(result);
                }
            }

            List<Result> panelItems = results.OrderByDescending(o => o.Score).Take(5).ToList();
            return panelItems;
        }

        private int Score(ControlPanelItem item, string query)
        {
            var scores = new List<int>();
            if (item.LocalizedString != null)
            {
                var score1 = StringMatcher.Score(item.LocalizedString, query);
                var socre2 = StringMatcher.ScoreForPinyin(item.LocalizedString, query);
                scores.Add(Math.Max(score1, socre2));
            }
            if (item.InfoTip != null)
            {
                // todo should we add pinyin score for infotip also?
                var score = StringMatcher.Score(item.InfoTip, query);
                scores.Add(score);
            }
            return scores.Max();
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_controlpanel_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_controlpanel_plugin_description");
        }
    }
}
