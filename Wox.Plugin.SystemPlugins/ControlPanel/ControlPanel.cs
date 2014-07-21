using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Wox.Infrastructure;

namespace Wox.Plugin.SystemPlugins.ControlPanel
{
    public class ControlPanel : BaseSystemPlugin
    {
        #region Properties

        private PluginInitContext context;

        public override string Description
        {
            get
            {
                return "Search within the Control Panel.";
            }
        }

        public override string ID
        {
            get { return "209621585B9B4D81813913C507C058C6"; }
        }

        public override string Name { get { return "Control Panel"; } }

        public override string IcoPath { get { return @"Images\ControlPanel.png"; } }

        private List<ControlPanelItem> controlPanelItems;
        private string iconFolder;
        private string fileType;

        #endregion Properties

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
            controlPanelItems = ControlPanelList.Create(48);
            iconFolder = @"Images\ControlPanelIcons\";
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

        protected override List<Result> QueryInternal(Query query)
        {
            if (query.RawQuery.EndsWith(" ") || query.RawQuery.Length <= 1) return new List<Result>();
            string myQuery = query.RawQuery.Trim();

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
                        IcoPath = "Images\\ControlPanelIcons\\" + item.GUID + fileType,
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
            panelItems.ForEach(o => o.Score = 0);
            return panelItems;
        }

        private bool MatchProgram(ControlPanelItem item, FuzzyMatcher matcher)
        {
            if (item.LocalizedString != null && (item.Score = matcher.Evaluate(item.LocalizedString).Score) > 0) return true;
            if (item.InfoTip != null && (item.Score = matcher.Evaluate(item.InfoTip).Score) > 0) return true;

            if (item.LocalizedString != null && (item.Score = matcher.Evaluate(item.LocalizedString.Unidecode()).Score) > 0) return true;

            return false;
        }
    }
}
