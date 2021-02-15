// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeShellPage : UserControl
    {
        /// <summary>
        /// Gets view model.
        /// </summary>
        public OobeShellViewModel ViewModel { get; } = new OobeShellViewModel();

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static OobeShellPage OobeShellHandler { get; set; }

        public ObservableCollection<OobePowerToysModule> Modules { get; }

        public OobeShellPage()
        {
            InitializeComponent();

            DataContext = ViewModel;
            OobeShellHandler = this;

            Modules = new ObservableCollection<OobePowerToysModule>();

            Modules.Insert((int)PowerToysModulesEnum.ColorPicker, new OobePowerToysModule() { ModuleName = "Color Picker", Tag = "ColorPicker", IsNew = false, NavIndex = 0, Icon = "\uEF3C", Image = "ms-appx:///Assets/Modules/ColorPicker.png", FluentIcon = "ms-appx:///Assets/FluentIcons/ColorPicker.png" });
            Modules.Insert((int)PowerToysModulesEnum.FancyZones, new OobePowerToysModule() { ModuleName = "FancyZones", Tag = "FancyZones", IsNew = false, NavIndex = 1, Icon = "\uE737", Image = "ms-appx:///Assets/Modules/FancyZones.png", FluentIcon = "ms-appx:///Assets/FluentIcons/FancyZones.png" });
            Modules.Insert((int)PowerToysModulesEnum.ImageResizer, new OobePowerToysModule() { ModuleName = "ImageResizer", Tag = "ImageResizer", IsNew = false, NavIndex = 2, Icon = "\uEB9F", Image = "ms-appx:///Assets/Modules/ImageResizer.png", FluentIcon = "ms-appx:///Assets/FluentIcons/ImageResizer.png" });
            Modules.Insert((int)PowerToysModulesEnum.KBM, new OobePowerToysModule() { ModuleName = "KBM", Tag = "KBM", IsNew = false, NavIndex = 0, Icon = "\uE765", Image = "ms-appx:///Assets/Modules/KBM.png", FluentIcon = "ms-appx:///Assets/FluentIcons/KBM.png" });
            Modules.Insert((int)PowerToysModulesEnum.Run, new OobePowerToysModule() { ModuleName = "PowerToys Run", Tag = "Run", IsNew = false, NavIndex = 2, Icon = "\uE773", Image = "ms-appx:///Assets/Modules/PowerLauncher.png", FluentIcon = "ms-appx:///Assets/FluentIcons/ColorPicker.png" });
            Modules.Insert((int)PowerToysModulesEnum.FileExplorer, new OobePowerToysModule() { ModuleName = "File explorer add-ons", Tag = "FileExplorer", IsNew = false, NavIndex = 1, Icon = "\uEC50", Image = "ms-appx:///Assets/Modules/PowerPreview.png", FluentIcon = "ms-appx:///Assets/FluentIcons/FancyZones.png" });
            Modules.Insert((int)PowerToysModulesEnum.PowerRename, new OobePowerToysModule() { ModuleName = "PowerRename", Tag = "PowerRename", IsNew = false, NavIndex = 2, Icon = "\uE8AC", Image = "ms-appx:///Assets/Modules/PowerRename.png", FluentIcon = "ms-appx:///Assets/FluentIcons/PowerRename.png" });
            Modules.Insert((int)PowerToysModulesEnum.ShortcutGuide, new OobePowerToysModule() { ModuleName = "Shortcut Guide", Tag = "ShortcutGuide", IsNew = false, NavIndex = 0, Icon = "\uEDA7", Image = "ms-appx:///Assets/Modules/ShortcutGuide.png", FluentIcon = "ms-appx:///Assets/FluentIcons/ImageResizer.png" });
            Modules.Insert((int)PowerToysModulesEnum.VideoConference, new OobePowerToysModule() { ModuleName = "Video Conference", Tag = "VideoConference", IsNew = true, NavIndex = 1, Icon = "\uEC50", Image = "ms-appx:///Assets/Modules/VideoConference.png", FluentIcon = "ms-appx:///Assets/FluentIcons/FancyZones.png" });
        }

        private void UserControl_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Navigate to first page
            }

        [SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "Params are required for event handler signature requirements.")]
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            OobePowerToysModule selectedItem = args.SelectedItem as OobePowerToysModule;
            switch ((string)selectedItem.Tag)
            {
                default: NavigationFrame.Navigate(typeof(OobeDumpPage)); break;
            }
        }
    }
}
