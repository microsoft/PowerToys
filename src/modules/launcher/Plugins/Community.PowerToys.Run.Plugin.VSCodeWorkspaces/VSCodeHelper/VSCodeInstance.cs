using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    public enum VSCodeVersion
    {
        Stable = 1,
        Insiders = 2,
        Exploration = 3
    }

    public class VSCodeInstance
    {
        public VSCodeVersion VSCodeVersion { get; set; }

        public string ExecutablePath { get; set; } = String.Empty;

        public string AppData { get; set; } = String.Empty;

        public ImageSource WorkspaceIcon(){ return WorkspaceIconBitMap; }

        public ImageSource RemoteIcon(){ return RemoteIconBitMap; }

        public BitmapImage WorkspaceIconBitMap { get; set; }

        public BitmapImage RemoteIconBitMap { get; set; }
    }
}
