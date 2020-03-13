using System.IO;
using System.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Wox.Infrastructure.Image;

namespace Wox.Plugin.WebSearch
{
    public class SearchSource : BaseModel
    {
        public const string DefaultIcon = "web_search.png";
        public string Title { get; set; }
        public string ActionKeyword { get; set; }

        [NotNull]
        public string Icon { get; set; } = DefaultIcon;

        /// <summary>
        /// All icon should be put under Images directory
        /// </summary>
        [NotNull]
        [JsonIgnore]
        internal string IconPath => Path.Combine(Main.ImagesDirectory, Icon);

        [JsonIgnore]
        public ImageSource Image => ImageLoader.Load(IconPath);

        public string Url { get; set; }
        public bool Enabled { get; set; }

        public SearchSource DeepCopy()
        {
            var webSearch = new SearchSource
            {
                Title = string.Copy(Title),
                ActionKeyword = string.Copy(ActionKeyword),
                Url = string.Copy(Url),
                Icon = string.Copy(Icon),
                Enabled = Enabled
            };
            return webSearch;
        }
    }
}