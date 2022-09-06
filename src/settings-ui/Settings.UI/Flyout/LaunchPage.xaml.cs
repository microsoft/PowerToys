// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Flyout
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using interop;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LaunchPage : Page
    {
        private ObservableCollection<FlyoutItem> menuItems;

        public LaunchPage()
        {
            this.InitializeComponent();
            menuItems = new ObservableCollection<FlyoutItem>();
            menuItems.Add(new FlyoutItem() { Label = "Color Picker", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png", Tag = "ColorPicker" });
            menuItems.Add(new FlyoutItem() { Label = "FancyZones", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png", Tag = "FancyZones" });
            menuItems.Add(new FlyoutItem() { Label = "PowerToys Run", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png", Tag = "Run" });
            menuItems.Add(new FlyoutItem() { Label = "Screen Ruler", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsScreenRuler.png", Tag = "MeasureTool" });
            menuItems.Add(new FlyoutItem() { Label = "Shortcut Guide", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png", Tag = "ShortcutGuide" });
            menuItems.Add(new FlyoutItem() { Label = "Text Extractor", Icon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerOCR.png", Tag = "PowerOCR" });
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            Button selectedButton = sender as Button;
            Frame selectedFrame = this.Parent as Frame;
        }

        private void ModuleButton_Click(object sender, RoutedEventArgs e)
        {
            Button selectedModuleBtn = sender as Button;
            switch ((string)selectedModuleBtn.Tag)
            {
                case "ColorPicker": // Launch ColorPicker
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowColorPickerSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "FancyZones": // Launch FancyZones Editor
                    break;
                case "Run": // Launch Run
                    using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerLauncherSharedEvent()))
                    {
                        eventHandle.Set();
                    }

                    break;
                case "MeasureTool": // Launch Screen Ruler
                    break;
                case "ShortcutGuide": // Launch Shortcut Guide
                    break;
                case "PowerOCR": // Launch Text Extractor
                    break;
            }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class FlyoutItem
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Label { get; set; }

        public string Icon { get; set; }

        public bool HasOptions { get; set; }

        public string Tag { get; set; }
    }
}
