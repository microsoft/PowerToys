// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ShellViewModel : Observable
    {
        private readonly KeyboardAccelerator altLeftKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu);

        private readonly KeyboardAccelerator backKeyboardAccelerator = BuildKeyboardAccelerator(VirtualKey.GoBack);

        private bool isBackEnabled;
        private bool showCloseMenu;
        private IList<KeyboardAccelerator> keyboardAccelerators;
        private NavigationView navigationView;
        private NavigationViewItem selected;
        private NavigationViewItem expanding;
        private ICommand loadedCommand;
        private ICommand itemInvokedCommand;
        private NavigationViewItem[] _fullListOfNavViewItems;
        private NavigationViewItem[] _moduleNavViewItems;
        private GeneralSettings _generalSettingsConfig;

        public bool IsBackEnabled
        {
            get => isBackEnabled;
            set => Set(ref isBackEnabled, value);
        }

        public bool ShowCloseMenu
        {
            get => showCloseMenu;
            set => Set(ref showCloseMenu, value);
        }

        public NavigationViewItem Selected
        {
            get => selected;
            set => Set(ref selected, value);
        }

        public NavigationViewItem Expanding
        {
            get { return expanding; }
            set { Set(ref expanding, value); }
        }

        public NavigationViewItem[] NavItems
        {
            get { return _moduleNavViewItems; }
        }

        public ICommand LoadedCommand => loadedCommand ?? (loadedCommand = new RelayCommand(OnLoaded));

        public ICommand ItemInvokedCommand => itemInvokedCommand ?? (itemInvokedCommand = new RelayCommand<NavigationViewItemInvokedEventArgs>(OnItemInvoked));

        public ShellViewModel(ISettingsRepository<GeneralSettings> settingsRepository)
        {
            _generalSettingsConfig = settingsRepository.SettingsConfig;
            ShowCloseMenu = !_generalSettingsConfig.ShowSysTrayIcon;
        }

        public void Initialize(Frame frame, NavigationView navigationView, IList<KeyboardAccelerator> keyboardAccelerators)
        {
            this.navigationView = navigationView;
            this.keyboardAccelerators = keyboardAccelerators;
            NavigationService.Frame = frame;
            NavigationService.NavigationFailed += Frame_NavigationFailed;
            NavigationService.Navigated += Frame_Navigated;
            this.navigationView.BackRequested += OnBackRequested;
            var topLevelItems = navigationView.MenuItems.OfType<NavigationViewItem>();
            _moduleNavViewItems = topLevelItems.SelectMany(menuItem => menuItem.MenuItems.OfType<NavigationViewItem>()).ToArray();
            _fullListOfNavViewItems = topLevelItems.Union(_moduleNavViewItems).ToArray();
        }

        private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
        {
            var keyboardAccelerator = new KeyboardAccelerator() { Key = key };
            if (modifiers.HasValue)
            {
                keyboardAccelerator.Modifiers = modifiers.Value;
            }

            keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;
            return keyboardAccelerator;
        }

        private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var result = NavigationService.GoBack();
            args.Handled = result;
        }

        private async void OnLoaded()
        {
            // Keyboard accelerators are added here to avoid showing 'Alt + left' tooltip on the page.
            // More info on tracking issue https://github.com/Microsoft/microsoft-ui-xaml/issues/8
            keyboardAccelerators.Add(altLeftKeyboardAccelerator);
            keyboardAccelerators.Add(backKeyboardAccelerator);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void OnItemInvoked(NavigationViewItemInvokedEventArgs args)
        {
            var pageType = args.InvokedItemContainer.GetValue(NavHelper.NavigateToProperty) as Type;

            if (pageType != null)
            {
                NavigationService.Navigate(pageType);
            }
        }

        private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            NavigationService.GoBack();
        }

        private void Frame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw e.Exception;
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            IsBackEnabled = NavigationService.CanGoBack;
            Selected = _fullListOfNavViewItems.FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));
        }

        private static bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType)
        {
            var pageType = menuItem.GetValue(NavHelper.NavigateToProperty) as Type;
            return pageType == sourcePageType;
        }
    }
}
