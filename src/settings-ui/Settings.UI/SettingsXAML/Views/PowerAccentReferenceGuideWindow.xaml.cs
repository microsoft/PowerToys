// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerAccentReferenceGuideWindow : WindowEx
    {
        public PowerAccentReferenceGuideViewModel ViewModel { get; }

        /// <param name="selectedCodes">Selected language codes, surfaced at the top of the guide.</param>
        public PowerAccentReferenceGuideWindow(string[] selectedCodes)
        {
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            ViewModel = new PowerAccentReferenceGuideViewModel(
                selectedCodes ?? [],
                ResourceLoaderInstance.ResourceLoader.GetString);

            InitializeComponent();

            // CollectionViewSource is a resource and cannot track ViewModel changes via
            // x:Bind, so set its Source manually.
            LanguagesViewSource.Source = ViewModel.FilteredGroups;
            ViewModel.FilteredGroupsReplaced += OnFilteredGroupsReplaced;

            Activated += Window_Activated_SetIcon;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            // Guard against an empty title: an empty native window title can fault the WinUI TitleBar.
            var windowTitle = ResourceLoaderInstance.ResourceLoader.GetString("QuickAccent_ReferenceGuide_WindowTitle");
            if (string.IsNullOrEmpty(windowTitle))
            {
                windowTitle = "Character reference guide";
            }

            Title = windowTitle;
            CenterOnScreen();
        }

        private void OnFilteredGroupsReplaced(object sender, System.EventArgs e)
        {
            // WinUI 3's CollectionViewSource does not observe grouped ObservableCollection mutations,
            // so swap to an empty collection and back. Using an empty collection rather than null
            // avoids a crash when the ListView holds a live view reference.
            var groups = ViewModel.FilteredGroups;
            LanguagesViewSource.Source = new ObservableCollection<ReferenceGroupModel>();
            LanguagesViewSource.Source = groups;

            Bindings.Update();
        }

        private void OnThemeChanged(object sender, ElementTheme theme)
        {
            WindowHelper.SetTheme(this, theme);
        }

        private void CenterOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var windowSize = AppWindow.Size;
                AppWindow.Move(new PointInt32
                {
                    X = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - windowSize.Width) / 2),
                    Y = displayArea.WorkArea.Y + ((displayArea.WorkArea.Height - windowSize.Height) / 2),
                });
            }
        }

        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // ListView can't tab into its items because the containers aren't tab stops,
            // so move focus from the search box to the first character button manually.
            if (e.Key != VirtualKey.Tab ||
                InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                return;
            }

            if (FocusManager.FindFirstFocusableElement(LanguagesListView) is Control first &&
                first.Focus(FocusState.Keyboard))
            {
                e.Handled = true;
            }
        }

        private void CharacterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: CharacterModel characterModel })
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(characterModel.Value);
                Clipboard.SetContent(dataPackage);
            }
        }

        private void WindowEx_Closed(object sender, WindowEventArgs args)
        {
            ViewModel.FilteredGroupsReplaced -= OnFilteredGroupsReplaced;
            App.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void Window_Activated_SetIcon(object sender, WindowActivatedEventArgs args)
        {
            AppWindow.SetIcon("Assets\\Settings\\Icons\\QuickAccent.ico");
        }
    }
}
