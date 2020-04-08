using Windows.UI.Xaml.Controls;

namespace PowerLauncher.UI
{
    public sealed partial class LauncherControl : UserControl
    {
        public LauncherControl()
        {
            this.InitializeComponent();
            ShellBarShadow.Receivers.Add(ShadowReceiverGrid);   
        }
    }
}