// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinRT;
using static Microsoft.PowerToys.Settings.UI.ViewModels.MouseWithoutBordersViewModel;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class MouseWithoutBordersPage : Page, IRefreshablePage
    {
        private const string MouseWithoutBordersDragDropCheckString = "MWB Device Drag Drop";

        private const string PowerToyName = "MouseWithoutBorders";

        private MouseWithoutBordersViewModel ViewModel { get; set; }

        private readonly IFileSystemWatcher watcher;

        public MouseWithoutBordersPage()
        {
            var settingsUtils = new SettingsUtils();
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
        }

        private void OnConfigFileUpdate()
        {
            // Note: FileSystemWatcher raise notification multiple times for single update operation.
            // Todo: Handle duplicate events either by somehow suppress them or re-read the configuration everytime since we will be updating the UI only if something is changed.
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.LoadUpdatedSettings())
                {
                    ViewModel.NotifyUpdatedSettings();
                }
            });
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

        public ICommand GenerateNewKeyCommand => new AsyncCommand(ViewModel.SubmitNewKeyRequestAsync);

        public ICommand CopyPCNameCommand => new RelayCommand(ViewModel.CopyMachineNameToClipboard);

        public ICommand ReconnectCommand => new AsyncCommand(ViewModel.SubmitReconnectRequestAsync);

        private void ShowConnectFields()
        {
            ViewModel.ConnectFieldsVisible = true;
        }

        private async Task Connect()
        {
            if (ConnectPCNameTextBox.Text.Length != 0 && ConnectSecurityKeyTextBox.Text.Length != 0)
            {
                string pcName = ConnectPCNameTextBox.Text;
                string securityKey = ConnectSecurityKeyTextBox.Text.Trim();

                await ViewModel.SubmitConnectionRequestAsync(pcName, securityKey);

                ConnectPCNameTextBox.Text = string.Empty;
                ConnectSecurityKeyTextBox.Text = string.Empty;
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
