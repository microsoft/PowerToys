using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PropertyChanged;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using static System.String;

namespace Wox.ViewModel
{
    [ImplementPropertyChanged]
    public class SettingWindowViewModel
    {
        public Settings Settings { get; set; }

        private readonly JsonStrorage<Settings> _storage;
        private readonly Dictionary<ISettingProvider, Control> _featureControls = new Dictionary<ISettingProvider, Control>();

        public Tab SelectedTab { get; set; } = Tab.General;
        public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);
        public PluginViewModel SelectedPlugin { get; set; }
        public IList<PluginViewModel> PluginViewModels
        {
            get
            {
                var plugins = PluginManager.AllPlugins;
                var settings = Settings.PluginSettings.Plugins;
                plugins.Sort((a, b) =>
                {
                    var d1 = settings[a.Metadata.ID].Disabled;
                    var d2 = settings[b.Metadata.ID].Disabled;
                    if (d1 == d2)
                    {
                        return Compare(a.Metadata.Name, b.Metadata.Name, StringComparison.CurrentCulture);
                    }
                    else
                    {
                        return d1.CompareTo(d2);
                    }
                });

                var metadatas = plugins.Select(p => new PluginViewModel
                {
                    PluginPair = p,
                    Metadata = p.Metadata,
                    Plugin = p.Plugin
                }).ToList();
                return metadatas;
            }
        }

        public Control SettingProvider
        {
            get
            {
                var settingProvider = SelectedPlugin.Plugin as ISettingProvider;
                if (settingProvider != null)
                {
                    Control control;
                    if (!_featureControls.TryGetValue(settingProvider, out control))
                    {
                        var multipleActionKeywordsProvider = settingProvider as IMultipleActionKeywords;
                        if (multipleActionKeywordsProvider != null)
                        {
                            multipleActionKeywordsProvider.ActionKeywordsChanged += (o, e) =>
                            {
                                // update in-memory data
                                PluginManager.UpdateActionKeywordForPlugin(SelectedPlugin.PluginPair, e.OldActionKeyword,
                                    e.NewActionKeyword);
                                // update persistant data
                                Settings.PluginSettings.UpdateActionKeyword(SelectedPlugin.Metadata);

                                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("succeed"));
                            };
                        }

                        _featureControls.Add(settingProvider, control = settingProvider.CreateSettingPanel());
                    }
                    control.HorizontalAlignment = HorizontalAlignment.Stretch;
                    control.VerticalAlignment = VerticalAlignment.Stretch;
                    return control;
                }
                else
                {
                    return new Control();
                }
            }
        }

        public SettingWindowViewModel()
        {
            _storage = new JsonStrorage<Settings>();
            Settings = _storage.Load();
        }




        public void Save()
        {
            _storage.Save();
        }

        public enum Tab
        {
            General = 0,
            Plugin = 1,
            Theme = 2,
            Hotkey = 3,
            Proxy = 4,
            About = 5
        }
    }
}
