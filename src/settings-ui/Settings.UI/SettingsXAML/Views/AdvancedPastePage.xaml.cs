// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : NavigablePage, IRefreshablePage
    {
        private AdvancedPasteViewModel ViewModel { get; set; }

        public ICommand SaveOpenAIKeyCommand => new RelayCommand(SaveOpenAIKey);

        public AdvancedPastePage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new AdvancedPasteViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();

            Loaded += (s, e) =>
            {
                ViewModel.OnPageLoaded();
                UpdateAdvancedAIUIVisibility();
                UpdatePasteAIUIVisibility();
            };
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void SaveOpenAIKey()
        {
            if (!string.IsNullOrEmpty(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text))
            {
                ViewModel.EnableAI(AdvancedPaste_EnableAIDialogOpenAIApiKey.Text);
            }
        }

        private async void AdvancedPaste_EnableAIButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            EnableAIDialog.PrimaryButtonText = resourceLoader.GetString("EnableAIDialog_SaveBtnText");
            EnableAIDialog.SecondaryButtonText = resourceLoader.GetString("EnableAIDialog_CancelBtnText");
            EnableAIDialog.PrimaryButtonCommand = SaveOpenAIKeyCommand;

            AdvancedPaste_EnableAIDialogOpenAIApiKey.Text = string.Empty;

            await ShowEnableDialogAsync();
        }

        private async Task ShowEnableDialogAsync()
        {
            await EnableAIDialog.ShowAsync();
        }

        private void AdvancedPaste_DisableAIButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DisableAI();
        }

        private void AdvancedPaste_EnableAIDialogOpenAIApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableAIDialog.IsPrimaryButtonEnabled = AdvancedPaste_EnableAIDialogOpenAIApiKey.Text.Length > 0;
        }

        public async void DeleteCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var customAction = GetBoundCustomAction(sender);
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            ContentDialog dialog = new()
            {
                XamlRoot = RootPage.XamlRoot,
                Title = customAction.Name,
                PrimaryButtonText = resourceLoader.GetString("Yes"),
                CloseButtonText = resourceLoader.GetString("No"),
                DefaultButton = ContentDialogButton.Primary,
                Content = new TextBlock() { Text = resourceLoader.GetString("Delete_Dialog_Description") },
            };

            dialog.PrimaryButtonClick += (_, _) => ViewModel.DeleteCustomAction(customAction);

            await dialog.ShowAsync();
        }

        private async void AddCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            CustomActionDialog.Title = resourceLoader.GetString("AddCustomAction");
            CustomActionDialog.DataContext = ViewModel.GetNewCustomAction(resourceLoader.GetString("AdvancedPasteUI_NewCustomActionPrefix"));
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionSave");
            await CustomActionDialog.ShowAsync();
        }

        private async void EditCustomActionButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            CustomActionDialog.Title = resourceLoader.GetString("EditCustomAction");
            CustomActionDialog.DataContext = GetBoundCustomAction(sender).Clone();
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionUpdate");
            await CustomActionDialog.ShowAsync();
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender));
            ViewModel.CustomActions.Move(index, index + 1);
        }

        private void ReorderButtonUp_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender));
            ViewModel.CustomActions.Move(index, index - 1);
        }

        private void CustomActionDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var dialogCustomAction = GetBoundCustomAction(sender);
            var existingCustomAction = ViewModel.CustomActions.FirstOrDefault(candidate => candidate.Id == dialogCustomAction.Id);

            if (existingCustomAction == null)
            {
                /* TO DO: Re-enable with SettingsExpander, not a P0 though
                ViewModel.AddCustomAction(dialogCustomAction);

                var element = (ContentPresenter)AdvancedPasteUIActions.ContainerFromIndex(CustomActions.Items.Count - 1);
                element.StartBringIntoView(new BringIntoViewOptions { VerticalOffset = -60, AnimationDesired = true });
                element.Focus(FocusState.Programmatic); */
            }
            else
            {
                existingCustomAction.Update(dialogCustomAction);
            }
        }

        private static AdvancedPasteCustomAction GetBoundCustomAction(object sender) => (AdvancedPasteCustomAction)((FrameworkElement)sender).DataContext;

        private void BrowsePasteAIModelPath_Click(object sender, RoutedEventArgs e)
        {
            // Use Win32 file dialog to work around FileOpenPicker issues with elevated permissions
            string selectedFile = PickFileDialog(
                "ONNX Model Files\0*.onnx\0All Files\0*.*\0",
                "Select ONNX Model File");

            if (!string.IsNullOrEmpty(selectedFile))
            {
                PasteAIModelPathTextBox.Text = selectedFile;
                ViewModel.PasteAIConfiguration.ModelPath = selectedFile;
            }
        }

        private static string PickFileDialog(string filter, string title, string initialDir = null, int initialFilter = 0)
        {
            // Use Win32 OpenFileName dialog as FileOpenPicker doesn't work with elevated permissions
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = filter;

            // Make buffer double MAX_PATH since it can use 2 chars per char
            openFileName.File = new string(new char[260 * 2]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[260 * 2]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.InitialDir = initialDir;
            openFileName.Title = title;
            openFileName.FilterIndex = initialFilter;
            openFileName.DefExt = null;
            openFileName.Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR; // OFN_NOCHANGEDIR flag is needed
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            openFileName.Hwnd = windowHandle;

            bool result = NativeMethods.GetOpenFileName(openFileName);
            if (result)
            {
                return openFileName.File;
            }

            return null;
        }

        private void ShowApiKeySavedMessage(string configType)
        {
            // This would typically show a TeachingTip or InfoBar
            // For now, we'll use a simple approach
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // In a real implementation, you'd want to show a proper notification
            System.Diagnostics.Debug.WriteLine($"{configType} API key saved successfully");
        }

        private void PasteAIServiceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePasteAIUIVisibility();
        }

        private void UpdateAdvancedAIUIVisibility()
        {
            if (AdvancedAIServiceTypeListView?.SelectedValue == null)
            {
                return;
            }

            string selectedType = AdvancedAIServiceTypeListView.SelectedValue.ToString();
            bool isAzureOpenAI = selectedType == "AzureOpenAI";

            AdvancedAIEndpointUrlTextBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
            AdvancedAIDeploymentNameTextBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePasteAIUIVisibility()
        {
            if (PasteAIServiceTypeListView?.SelectedValue == null)
            {
                return;
            }

            string selectedType = PasteAIServiceTypeListView.SelectedValue.ToString();
            bool isAzureOpenAI = selectedType == "AzureOpenAI";
            bool isOnnx = selectedType == "Onnx";

            PasteAIApiKeyPasswordBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
            PasteAIDeploymentNameTextBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModelPanel.Visibility = isOnnx ? Visibility.Visible : Visibility.Collapsed;
            PasteAIApiKeyPasswordBox.Visibility = !isOnnx ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void AdvancedAIProviderConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            await AdvancedAIProviderConfigurationDialog.ShowAsync();
        }

        private void AdvancedAIProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!string.IsNullOrEmpty(AdvancedAIApiKeyPasswordBox.Password))
            {
                string serviceType = AdvancedAIServiceTypeListView.SelectedValue?.ToString() ?? "OpenAI";
                ViewModel.SaveAdvancedAICredential(serviceType, AdvancedAIApiKeyPasswordBox.Password);
                AdvancedAIApiKeyPasswordBox.Password = string.Empty;

                // Show success message
                ShowApiKeySavedMessage("Advanced AI");
            }
        }

        private void PasteAIProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!string.IsNullOrEmpty(PasteAIApiKeyPasswordBox.Password))
            {
                string serviceType = PasteAIServiceTypeListView.SelectedValue?.ToString() ?? "OpenAI";
                ViewModel.SavePasteAICredential(serviceType, PasteAIApiKeyPasswordBox.Password);
                PasteAIApiKeyPasswordBox.Password = string.Empty;

                // Show success message
                ShowApiKeySavedMessage("Paste AI");
            }
        }

        private async void PasteAIProviderConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            await PasteAIProviderConfigurationDialog.ShowAsync();
        }

        private void AdvancedAIServiceTypeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAdvancedAIUIVisibility();
        }

        private void PasteAIServiceTypeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePasteAIUIVisibility();
        }
    }
}
