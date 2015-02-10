using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Wox.Infrastructure;

namespace Wox.Plugin.ControlPanel
{
    public class ControlPanel : IPlugin,IPluginI18n
    {
        private PluginInitContext context;
        private List<ControlPanelItem> controlPanelItems = new List<ControlPanelItem>();
        private string iconFolder;
        private string fileType;

        public  void Init(PluginInitContext context)
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
        }

        public List<Result> Query(Query query)
        {
            string myQuery = query.Search.Trim();
            List<Result> results = new List<Result>();

            foreach (var item in controlPanelItems)
            {
                var fuzzyMather = FuzzyMatcher.Create(myQuery);
                if (MatchProgram(item, fuzzyMather))
                {
                    results.Add(new Result()
                    {
                        Title = item.LocalizedString,
                        SubTitle = item.InfoTip,
                        Score = item.Score,
                        IcoPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, @"Images\\ControlPanelIcons\\" + item.GUID + fileType),
                        Action = e =>
                        {
                            try
                            {
                                Process.Start(item.ExecutablePath);
                            }
                            catch (Exception)
                            {
                                //Silently Fail for now..
                            }
                            return true;
                        }
                    });
                }
            }

            List<Result> panelItems = results.OrderByDescending(o => o.Score).Take(5).ToList();
            return panelItems;
        }

        private bool MatchProgram(ControlPanelItem item, FuzzyMatcher matcher)
        {
            if (item.LocalizedString != null && (item.Score = matcher.Evaluate(item.LocalizedString).Score) > 0) return true;
            if (item.InfoTip != null && (item.Score = matcher.Evaluate(item.InfoTip).Score) > 0) return true;

            if (item.LocalizedString != null && (item.Score = matcher.Evaluate(item.LocalizedString.Unidecode()).Score) > 0) return true;

            return false;
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
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
