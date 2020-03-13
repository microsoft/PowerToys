using System.Windows;
using System.Windows.Media;
using Wox.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure.Image;

namespace Wox.ViewModel
{
    public class PluginViewModel : BaseModel
    {
        public PluginPair PluginPair { get; set; }

        private readonly Internationalization _translator = InternationalizationManager.Instance;

        public ImageSource Image => ImageLoader.Load(PluginPair.Metadata.IcoPath);
        public Visibility ActionKeywordsVisibility => PluginPair.Metadata.ActionKeywords.Count > 1 ? Visibility.Collapsed : Visibility.Visible;
        public string InitilizaTime => string.Format(_translator.GetTranslation("plugin_init_time"), PluginPair.Metadata.InitTime);
        public string QueryTime => string.Format(_translator.GetTranslation("plugin_query_time"), PluginPair.Metadata.AvgQueryTime);
        public string ActionKeywordsText => string.Join(Query.ActionKeywordSeperater, PluginPair.Metadata.ActionKeywords);
    }
}
