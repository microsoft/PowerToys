using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.CMD
{
    public partial class CMDSetting : UserControl
    {
        public CMDSetting()
        {
            InitializeComponent();
        }

        private void CMDSetting_OnLoaded(object sender, RoutedEventArgs re)
        {
            cbReplaceWinR.IsChecked = UserSettingStorage.Instance.ReplaceWinR;
            cbLeaveCmdOpen.IsChecked = UserSettingStorage.Instance.LeaveCmdOpen;

            cbLeaveCmdOpen.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.LeaveCmdOpen = true;
                UserSettingStorage.Instance.Save();
            };

            cbLeaveCmdOpen.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.LeaveCmdOpen = false;
                UserSettingStorage.Instance.Save();
            };

            cbReplaceWinR.Checked += (o, e) =>
            {
                UserSettingStorage.Instance.ReplaceWinR = true;
                UserSettingStorage.Instance.Save();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                UserSettingStorage.Instance.ReplaceWinR = false;
                UserSettingStorage.Instance.Save();
            };
        }
    }
}
