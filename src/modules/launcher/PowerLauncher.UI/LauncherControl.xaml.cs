using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
