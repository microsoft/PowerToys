// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WinRT;

using static Microsoft.PowerToys.Settings.UI.ViewModels.MouseWithoutBordersViewModel;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class MouseWithoutBordersPage : NavigablePage, IRefreshablePage
    {
        private const string MouseWithoutBordersDragDropCheckString = "MWB Device Drag Drop";

        private const string PowerToyName = "MouseWithoutBorders";

        private MouseWithoutBordersViewModel ViewModel { get; set; }

        private readonly IFileSystemWatcher watcher;
        private int _configRefreshPending;
        private int _configRefreshRequested;

        public MouseWithoutBordersPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new MouseWithoutBordersViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage,
                DispatcherQueue);

            watcher = Helper.GetFileWatcher(
                PowerToyName,
                "settings.json",
                OnConfigFileUpdate);

            DataContext = ViewModel;
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                ClearPairingUI();
                ViewModel.OnPageLoaded();
                if (ViewModel.LoadUpdatedSettings())
                {
                    ViewModel.NotifyUpdatedSettings();
                }

                await ViewModel.LoadPairingKeyAsync();
            };
            Unloaded += (s, e) => ClearPairingUI();
        }

        private void OnConfigFileUpdate()
        {
            Interlocked.Exchange(ref _configRefreshRequested, 1);
            if (Interlocked.Exchange(ref _configRefreshPending, 1) != 0)
            {
                return;
            }

            if (!this.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    while (Interlocked.Exchange(ref _configRefreshRequested, 0) != 0 && IsLoaded)
                    {
                        if (ViewModel.LoadUpdatedSettings())
                        {
                            ViewModel.NotifyUpdatedSettings();
                            await ViewModel.LoadPairingKeyAsync();
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _configRefreshPending, 0);
                    if (Volatile.Read(ref _configRefreshRequested) != 0 && IsLoaded)
                    {
                        OnConfigFileUpdate();
                    }
                }
            }))
            {
                Interlocked.Exchange(ref _configRefreshPending, 0);
            }
        }

        private static T GetChildOfType<T>(DependencyObject depObj, string tag)
            where T : FrameworkElement
        {
            if (depObj == null)
            {
                return null;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child, tag);
                if (result != null && (string)result.Tag == tag)
                {
                    return result;
                }
            }

            return null;
        }

        private int GetDeviceIndex(Border b)
        {
            return b.DataContext.As<IndexedItem<DeviceViewModel>>().Index;
        }

        private void Device_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            args.Data.RequestedOperation = DataPackageOperation.Move;
            args.Data.Properties.Add("check-usage", MouseWithoutBordersDragDropCheckString);
            args.Data.Properties.Add("index", GetDeviceIndex((Border)sender));
        }

        private void Device_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Properties.TryGetValue("check-usage", out object checkUsage))
            {
                // Guard against values dragged from somewhere else
                if (!((string)checkUsage).Equals(MouseWithoutBordersDragDropCheckString, StringComparison.Ordinal))
                {
                    return;
                }
            }
            else
            {
                return;
            }

            if (!e.DataView.Properties.TryGetValue("index", out object boxIndex))
            {
                return;
            }

            var draggedDeviceIndex = (int)boxIndex;

            if (draggedDeviceIndex < 0 || draggedDeviceIndex >= ViewModel.MachineMatrixString.Count)
            {
                return;
            }

            var targetDeviceIndex = GetDeviceIndex((Border)e.OriginalSource);

            ViewModel.MachineMatrixString.Swap(draggedDeviceIndex, targetDeviceIndex);
            var itemsControl = (ItemsControl)FindName("DevicesItemsControl");
            var binding = itemsControl.GetBindingExpression(ItemsControl.ItemsSourceProperty);
            binding.UpdateSource();
        }

        private void Device_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        public ICommand ShowConnectFieldsCommand => new RelayCommand(ShowConnectFields);

        public ICommand ConnectCommand => new AsyncCommand(Connect);

        public ICommand GenerateNewKeyCommand => new AsyncCommand(() => TryExecuteSettingsCommandAsync(ViewModel.SubmitNewKeyRequestAsync, SetSettingsSyncErrorVisibility));

        public ICommand CopyPCNameCommand => new RelayCommand(ViewModel.CopyMachineNameToClipboard);

        public ICommand ReconnectCommand => new AsyncCommand(() => TryExecuteSettingsCommandAsync(ViewModel.SubmitReconnectRequestAsync, SetSettingsSyncErrorVisibility));

        private void ShowConnectFields()
        {
            if (ViewModel.CanConnectPairing)
            {
                ViewModel.ConnectFieldsVisible = true;
            }
        }

        internal static async Task<bool> TryExecuteSettingsCommandAsync(Func<Task> command, Action<bool> setErrorVisible, Action onSuccess = null)
        {
            setErrorVisible(false);
            try
            {
                await command();
            }
            catch (Exception ex)
            {
                // Remote exception messages can contain the submitted security key.
                Logger.LogError($"Mouse Without Borders Settings command failed ({ex.GetType().Name}).");
                setErrorVisible(true);
                return false;
            }

            onSuccess?.Invoke();
            return true;
        }

        private void SetSettingsSyncErrorVisibility(bool visible)
        {
            SettingsSyncErrorInfoBar.IsOpen = visible;
        }

        private async Task Connect()
        {
            var pcName = ConnectPCNameTextBox.Text;
            var securityKey = ConnectSecurityKeyTextBox.Text.Trim();
            if (await ViewModel.SubmitConnectionRequestAsync(pcName, securityKey))
            {
                ConnectPCNameTextBox.Text = string.Empty;
                ConnectSecurityKeyTextBox.Text = string.Empty;
                ViewModel.ConnectFieldsVisible = false;
            }
        }

        private async void ServiceMode_Toggled(object sender, RoutedEventArgs e)
        {
            ClearPairingUI();
            await ViewModel.LoadPairingKeyAsync();
        }

        private void ClearPairingUI()
        {
            ViewModel.ResetPairingUiState();
            if (ConnectPCNameTextBox != null)
            {
                ConnectPCNameTextBox.Text = string.Empty;
            }

            if (ConnectSecurityKeyTextBox != null)
            {
                ConnectSecurityKeyTextBox.Text = string.Empty;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
