using Microsoft.PowerToys.Settings.UI.Lib;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PowerPreview : Page
    {
        private const string POWERTOY_NAME = "File Explorer Preview";

        public PowerPreview()
        {
            this.InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PowerPreviewSettings settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
            base.OnNavigatedTo(e);

            // load settings to view.
            ToggleSwitch_Preview_SVG.IsOn = settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value;
            ToggleSwitch_Preview_MD.IsOn = settings.properties.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID.value;
        }

        private void ToggleSwitch_Preview_SVG_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerPreviewSettings settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
                settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value = swt.IsOn;

                if (ShellPage.Run_OnStartUp_Callback != null)
                {
                    ShellPage.Run_OnStartUp_Callback(settings.ToString());
                }
            }
        }

        private void ToggleSwitch_Preview_MD_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerPreviewSettings settings = SettingsUtils.GetSettings<PowerPreviewSettings>(POWERTOY_NAME);
                settings.properties.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL.value = swt.IsOn;

                if (ShellPage.Run_OnStartUp_Callback != null)
                {
                    ShellPage.Run_OnStartUp_Callback(settings.ToString());
                }
            }
        }
    }
}
