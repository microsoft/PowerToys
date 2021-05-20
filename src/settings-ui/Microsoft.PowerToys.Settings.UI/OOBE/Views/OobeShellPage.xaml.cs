// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeShellPage : UserControl
    {
        public static Func<string> RunSharedEventCallback { get; set; }

        public static void SetRunSharedEventCallback(Func<string> implementation)
        {
            RunSharedEventCallback = implementation;
        }

        public static Func<string> ColorPickerSharedEventCallback { get; set; }

        public static void SetColorPickerSharedEventCallback(Func<string> implementation)
        {
            ColorPickerSharedEventCallback = implementation;
        }

        public static Action<Type> OpenMainWindowCallback { get; set; }

        public static void SetOpenMainWindowCallback(Action<Type> implementation)
        {
            OpenMainWindowCallback = implementation;
        }

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
            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();

            Modules.Insert((int)PowerToysModulesEnum.Overview, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_Welcome"),
                Tag = "Overview",
                IsNew = false,
                Icon = "\uEF3C",
                Image = "ms-appx:///Assets/Modules/ColorPicker.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToys.png",
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/OOBEPTHero.png",
                DescriptionLink = "https://aka.ms/PowerToysOverview",
                Link = "https://github.com/microsoft/PowerToys/releases/",
            });
            Modules.Insert((int)PowerToysModulesEnum.ColorPicker, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_ColorPicker"),
                Tag = "ColorPicker",
                IsNew = false,
                Icon = "\uEF3C",
                Image = "ms-appx:///Assets/Modules/ColorPicker.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsColorPicker.png",
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/ColorPicker.gif",
                Description = loader.GetString("Oobe_ColorPicker_Description"),
                Link = "https://aka.ms/PowerToysOverview_ColorPicker",
            });
            Modules.Insert((int)PowerToysModulesEnum.FancyZones, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_FancyZones"),
                Tag = "FancyZones",
                IsNew = false,
                Icon = "\uE737",
                Image = "ms-appx:///Assets/Modules/FancyZones.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsFancyZones.png",
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/FancyZones.gif",
                Description = loader.GetString("Oobe_FancyZones_Description"),
                Link = "https://aka.ms/PowerToysOverview_FancyZones",
            });
            Modules.Insert((int)PowerToysModulesEnum.FileExplorer, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_FileExplorer"),
                Tag = "FileExplorer",
                IsNew = false,
                Icon = "\uEC50",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsFileExplorerPreview.png",
                Image = "ms-appx:///Assets/Modules/PowerPreview.png",
                Description = loader.GetString("Oobe_FileExplorer_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/FileExplorer.png",
                Link = "https://aka.ms/PowerToysOverview_FileExplorerAddOns",
            });
            Modules.Insert((int)PowerToysModulesEnum.ImageResizer, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_ImageResizer"),
                Tag = "ImageResizer",
                IsNew = false,
                Icon = "\uEB9F",
                Image = "ms-appx:///Assets/Modules/ImageResizer.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsImageResizer.png",
                Description = loader.GetString("Oobe_ImageResizer_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/ImageResizer.gif",
                Link = "https://aka.ms/PowerToysOverview_ImageResizer",
            });
            Modules.Insert((int)PowerToysModulesEnum.KBM, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_KBM"),
                Tag = "KBM",
                IsNew = false,
                Icon = "\uE765",
                Image = "ms-appx:///Assets/Modules/KeyboardManager.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsKeyboardManager.png",
                Description = loader.GetString("Oobe_KBM_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/KBM.gif",
                Link = "https://aka.ms/PowerToysOverview_KeyboardManager",
            });
            Modules.Insert((int)PowerToysModulesEnum.PowerRename, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_PowerRename"),
                Tag = "PowerRename",
                IsNew = false,
                Icon = "\uE8AC",
                Image = "ms-appx:///Assets/Modules/PowerRename.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerRename.png",
                Description = loader.GetString("Oobe_PowerRename_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/PowerRename.gif",
                Link = "https://aka.ms/PowerToysOverview_PowerRename",
            });
            Modules.Insert((int)PowerToysModulesEnum.Run, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_Run"),
                Tag = "Run",
                IsNew = false,
                Icon = "\uE773",
                Image = "ms-appx:///Assets/Modules/PowerLauncher.png",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsPowerToysRun.png",
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/Run.gif",
                Description = loader.GetString("Oobe_PowerRun_Description"),
                Link = "https://aka.ms/PowerToysOverview_PowerToysRun",
            });
            Modules.Insert((int)PowerToysModulesEnum.ShortcutGuide, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_ShortcutGuide"),
                Tag = "ShortcutGuide",
                IsNew = false,
                Icon = "\uEDA7",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsShortcutGuide.png",
                Image = "ms-appx:///Assets/Modules/ShortcutGuide.png",
                Description = loader.GetString("Oobe_ShortcutGuide_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/OOBEShortcutGuide.png",
                Link = "https://aka.ms/PowerToysOverview_ShortcutGuide",
            });
            /* Modules.Insert((int)PowerToysModulesEnum.VideoConference, new OobePowerToysModule()
            {
                ModuleName = loader.GetString("Oobe_VideoConference"),
                Tag = "VideoConference",
                IsNew = true,
                Icon = "\uEC50",
                FluentIcon = "ms-appx:///Assets/FluentIcons/FluentIconsVideoConferenceMute.png",
                Image = "ms-appx:///Assets/Modules/VideoConference.png",
                Description = loader.GetString("Oobe_VideoConference_Description"),
                PreviewImageSource = "ms-appx:///Assets/Modules/OOBE/VideoConferenceMute.png",
                Link = "https://aka.ms/PowerToysOverview_VideoConference",
            }); */
        }

        public void OnClosing()
        {
            if (NavigationView.SelectedItem != null)
            {
                ((OobePowerToysModule)NavigationView.SelectedItem).LogClosingModuleEvent();
            }
        }

        private void UserControl_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Modules.Count > 0)
            {
                NavigationView.SelectedItem = Modules[(int)PowerToysModulesEnum.Overview];
            }
        }

        [SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "Params are required for event handler signature requirements.")]
        private void NavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            OobePowerToysModule selectedItem = args.SelectedItem as OobePowerToysModule;

            switch (selectedItem.Tag)
            {
                case "Overview": NavigationFrame.Navigate(typeof(OobeOverview)); break;
                case "ColorPicker": NavigationFrame.Navigate(typeof(OobeColorPicker)); break;
                case "FancyZones": NavigationFrame.Navigate(typeof(OobeFancyZones)); break;
                case "Run": NavigationFrame.Navigate(typeof(OobeRun)); break;
                case "ImageResizer": NavigationFrame.Navigate(typeof(OobeImageResizer)); break;
                case "KBM": NavigationFrame.Navigate(typeof(OobeKBM)); break;
                case "PowerRename": NavigationFrame.Navigate(typeof(OobePowerRename)); break;
                case "FileExplorer": NavigationFrame.Navigate(typeof(OobeFileExplorer)); break;
                case "ShortcutGuide": NavigationFrame.Navigate(typeof(OobeShortcutGuide)); break;
                case "VideoConference": NavigationFrame.Navigate(typeof(OobeVideoConference)); break;
            }
        }
    }
}
