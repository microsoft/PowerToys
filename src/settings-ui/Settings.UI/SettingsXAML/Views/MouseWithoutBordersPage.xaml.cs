// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI;
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

            Loaded += (s, e) => ViewModel.OnPageLoaded();

            ViewModel.ConnectionSucceeded += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ConnectPCNameTextBox.Text = string.Empty;
                    ConnectSecurityKeyTextBox.Text = string.Empty;
                    ClearFieldError(ConnectPCNameTextBox);
                    ClearFieldError(ConnectSecurityKeyTextBox);
                });
            };

            ViewModel.ConnectionFailed += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var target = e.Target == ConnectFailureTarget.SecurityKey ? ConnectSecurityKeyTextBox : ConnectPCNameTextBox;
                    SetFieldError(target, e.Message);
                });
            };

            ConnectPCNameTextBox.TextChanged += (s, e) => ClearFieldError(ConnectPCNameTextBox);
            ConnectSecurityKeyTextBox.TextChanged += (s, e) => ClearFieldError(ConnectSecurityKeyTextBox);
        }

        private static void SetFieldError(TextBox textBox, string message)
        {
            textBox.BorderBrush = new SolidColorBrush(Colors.Red);
            textBox.BorderThickness = new Thickness(2);
            ToolTipService.SetToolTip(textBox, message);
        }

        private static void ClearFieldError(TextBox textBox)
        {
            textBox.ClearValue(Control.BorderBrushProperty);
            textBox.ClearValue(Control.BorderThicknessProperty);
            ToolTipService.SetToolTip(textBox, null);
        }

        private void OnConfigFileUpdate()
        {
            // Note: FileSystemWatcher raise notification multiple times for single update operation.
            // Todo: Handle duplicate events either by somehow suppress them or re-read the configuration every time since we will be updating the UI only if something is changed.
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

        public ICommand ConnectCommand => new AsyncCommand(Connect);

        public ICommand GenerateNewKeyCommand => new AsyncCommand(ViewModel.SubmitNewKeyRequestAsync);

        public ICommand CopyPCNameCommand => new RelayCommand(ViewModel.CopyMachineNameToClipboard);

        public ICommand CopySecurityKeyCommand => new RelayCommand(ViewModel.CopySecurityKeyToClipboard);

        public ICommand ReconnectCommand => new AsyncCommand(ViewModel.SubmitReconnectRequestAsync);

        private async Task Connect()
        {
            var emptyFields = new List<TextBox>();
            if (ConnectSecurityKeyTextBox.Text.Length == 0)
            {
                emptyFields.Add(ConnectSecurityKeyTextBox);
            }

            if (ConnectPCNameTextBox.Text.Length == 0)
            {
                emptyFields.Add(ConnectPCNameTextBox);
            }

            if (emptyFields.Count != 0)
            {
                await FlashRequiredFieldsAsync(emptyFields);
                return;
            }

            ClearFieldError(ConnectPCNameTextBox);
            ClearFieldError(ConnectSecurityKeyTextBox);

            string pcName = ConnectPCNameTextBox.Text;
            string securityKey = ConnectSecurityKeyTextBox.Text.Trim();

            // Fields are cleared only once ViewModel confirms the connection succeeded
            // (ConnectionSucceeded event), so a failed attempt keeps the values and
            // surfaces an error instead of silently wiping what the user typed.
            await ViewModel.SubmitConnectionRequestAsync(pcName, securityKey);
        }

        private static async Task FlashRequiredFieldsAsync(List<TextBox> textBoxes)
        {
            var flashBrush = new SolidColorBrush(Colors.Red);
            var originalBrushes = new Brush[textBoxes.Count];
            var originalThicknesses = new Thickness[textBoxes.Count];

            for (int i = 0; i < textBoxes.Count; i++)
            {
                originalBrushes[i] = textBoxes[i].BorderBrush;
                originalThicknesses[i] = textBoxes[i].BorderThickness;
            }

            for (int flash = 0; flash < 3; flash++)
            {
                foreach (var textBox in textBoxes)
                {
                    textBox.BorderBrush = flashBrush;
                    textBox.BorderThickness = new Thickness(2);
                }

                await Task.Delay(150);

                for (int i = 0; i < textBoxes.Count; i++)
                {
                    textBoxes[i].BorderBrush = originalBrushes[i];
                    textBoxes[i].BorderThickness = originalThicknesses[i];
                }

                await Task.Delay(150);
            }
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
