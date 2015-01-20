using System.Windows;
using System.Windows.Controls;

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
            cbReplaceWinR.IsChecked = CMDStorage.Instance.ReplaceWinR;
            cbLeaveCmdOpen.IsChecked = CMDStorage.Instance.LeaveCmdOpen;

            cbLeaveCmdOpen.Checked += (o, e) =>
            {
                CMDStorage.Instance.LeaveCmdOpen = true;
                CMDStorage.Instance.Save();
            };

            cbLeaveCmdOpen.Unchecked += (o, e) =>
            {
                CMDStorage.Instance.LeaveCmdOpen = false;
                CMDStorage.Instance.Save();
            };

            cbReplaceWinR.Checked += (o, e) =>
            {
                CMDStorage.Instance.ReplaceWinR = true;
                CMDStorage.Instance.Save();
            };
            cbReplaceWinR.Unchecked += (o, e) =>
            {
                CMDStorage.Instance.ReplaceWinR = false;
                CMDStorage.Instance.Save();
            };
        }
    }
}
