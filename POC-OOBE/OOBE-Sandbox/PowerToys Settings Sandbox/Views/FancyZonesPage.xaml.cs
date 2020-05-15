using System;
using System.Numerics;
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
using muxc = Microsoft.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace PowerToys_Settings_Sandbox.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FancyZonesPage : Page
    {
        public FancyZonesPage()
        {
            this.InitializeComponent();
        }

        //This method toggles the image preview of the Module Overview link of the "About Feature" section
        private void ToggleModuleOverviewTip(object sender, PointerRoutedEventArgs e)
        {
            if (ModuleOverViewImage.Visibility.Equals((Visibility)1))
            {
                ModuleOverViewImage.Visibility = 0;
            }
            else
            {
                ModuleOverViewImage.Visibility = (Visibility)1;
            }
        }
    }
}
