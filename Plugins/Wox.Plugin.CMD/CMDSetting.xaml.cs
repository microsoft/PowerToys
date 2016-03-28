using System.Windows;
using System.Windows.Controls;

namespace Wox.Plugin.CMD
{
    public partial class CMDSetting : UserControl
    {
        private readonly CMDStorage _settings;

        public CMDSetting(CMDStorage settings)
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
                _settings.Save();
            };

            cbLeaveCmdOpen.Unchecked += (o, e) =>
            {
                _settings.LeaveCmdOpen = false;
                _settings.Save();
            };

            cbReplaceWinR.Checked += (o, e) =>
            {
                _settings.ReplaceWinR = true;
                _settings.Save();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                _settings.ReplaceWinR = false;
                _settings.Save();
            };
        }
    }
}
