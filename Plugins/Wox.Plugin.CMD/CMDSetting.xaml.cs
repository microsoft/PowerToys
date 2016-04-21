using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.CMD
{
    public partial class CMDSetting : UserControl
    {
        private readonly CMDHistory _settings;

        public CMDSetting(CMDHistory settings)
        {
            InitializeComponent();
            _settings = settings;
        }

        private void CMDSetting_OnLoaded(object sender, RoutedEventArgs re)
        {
            cbReplaceWinR.IsChecked = _settings.ReplaceWinR;
            cbLeaveCmdOpen.IsChecked = _settings.LeaveCmdOpen;

            cbLeaveCmdOpen.Checked += (o, e) =>
            {
                _settings.LeaveCmdOpen = true;
            };

            cbLeaveCmdOpen.Unchecked += (o, e) =>
            {
                _settings.LeaveCmdOpen = false;
            };

            cbReplaceWinR.Checked += (o, e) =>
            {
                _settings.ReplaceWinR = true;
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                _settings.ReplaceWinR = false;
            };
        }
    }
}
