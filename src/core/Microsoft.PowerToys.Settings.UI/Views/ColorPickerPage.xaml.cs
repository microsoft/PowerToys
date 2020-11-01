// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class ColorPickerPage : Page
    {
        public ColorPickerViewModel ViewModel { get; set; }

        public ColorPickerPage()
        {
            var settingsUtils = new SettingsUtils(new SystemIOProvider());
            ViewModel = new ColorPickerViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        // TO DO - this needs to go
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CA2227 // Collection properties should be read only
        public System.Collections.ObjectModel.ObservableCollection<ColorFormat> ColorFormats
#pragma warning restore CA2227 // Collection properties should be read only
#pragma warning restore CA1822 // Mark members as static
        {
            get
            {
                System.Collections.ObjectModel.ObservableCollection<ColorFormat> f = new System.Collections.ObjectModel.ObservableCollection<ColorFormat>();
                f.Add(new ColorFormat() { Name = "HEX", Example = "#EF68FF", IsShown = true });
                f.Add(new ColorFormat() { Name = "RGB", Example = "rgb(239, 104, 255)", IsShown = true });
                f.Add(new ColorFormat() { Name = "HSL", Example = "hsl(294, 100%, 70%)", IsShown = false });
                f.Add(new ColorFormat() { Name = "HSV", Example = "hsv(294, 59%, 100%)", IsShown = false });
                f.Add(new ColorFormat() { Name = "CMYK", Example = "cmyk(6%, 59%, 0%, 0%)", IsShown = true });
                return f;
            }

            set
            {
            }
        }
    }

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable SA1402 // File may only contain a single type
    public class ColorFormat
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore CA1034 // Nested types should not be visible
    {
        public string Name { get; set; }

        public string Example { get; set; }

        public bool IsShown { get; set; }
    }
}
