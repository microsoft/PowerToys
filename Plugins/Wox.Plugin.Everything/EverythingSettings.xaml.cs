using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.Everything
{
    public partial class EverythingSettings : UserControl
    {
        private readonly Settings _settings;

        public EverythingSettings(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
        }

        private void View_Loaded(object sender, RoutedEventArgs re)
        {
            UseLocationAsWorkingDir.IsChecked = _settings.UseLocationAsWorkingDir;

            UseLocationAsWorkingDir.Checked += (o, e) =>
            {
                _settings.UseLocationAsWorkingDir = true;
            };

            UseLocationAsWorkingDir.Unchecked += (o, e) =>
            {
                _settings.UseLocationAsWorkingDir = false;
            };
        }
    }
}
