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

        public ICommand EnableAdvancedPasteAICommand => new RelayCommand(EnableAdvancedPasteAI);

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

        private void EnableAdvancedPasteAI() => ViewModel.EnableAI();

        private void AdvancedPaste_EnableAIToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var toggle = (ToggleSwitch)sender;

            if (toggle.IsOn)
            {
                ViewModel.EnableAI();
            }
            else
            {
                ViewModel.DisableAI();
            }
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

            bool showEndpoint = string.Equals(selectedType, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "AzureAIInference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "Mistral", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "HuggingFace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "Ollama", StringComparison.OrdinalIgnoreCase);
            bool showDeployment = string.Equals(selectedType, "AzureOpenAI", StringComparison.OrdinalIgnoreCase);
            bool requiresApiKey = RequiresApiKeyForService(selectedType);
            bool showModerationToggle = string.Equals(selectedType, "OpenAI", StringComparison.OrdinalIgnoreCase);

            if (ViewModel.AdvancedAIConfiguration is not null)
            {
                ViewModel.AdvancedAIConfiguration.EndpointUrl = ViewModel.GetAdvancedAIEndpoint(selectedType);
            }

            AdvancedAIEndpointUrlTextBox.Visibility = showEndpoint ? Visibility.Visible : Visibility.Collapsed;
            AdvancedAIDeploymentNameTextBox.Visibility = showDeployment ? Visibility.Visible : Visibility.Collapsed;
            AdvancedAIModerationToggle.Visibility = showModerationToggle ? Visibility.Visible : Visibility.Collapsed;
            AdvancedAIApiKeyPasswordBox.Visibility = requiresApiKey ? Visibility.Visible : Visibility.Collapsed;

            if (requiresApiKey)
            {
                AdvancedAIApiKeyPasswordBox.Password = ViewModel.GetAdvancedAIApiKey(selectedType);
            }
            else
            {
                AdvancedAIApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private void UpdatePasteAIUIVisibility()
        {
            if (PasteAIServiceTypeListView?.SelectedValue == null)
            {
                return;
            }

            string selectedType = PasteAIServiceTypeListView.SelectedValue.ToString();

            bool isOnnx = string.Equals(selectedType, "Onnx", StringComparison.OrdinalIgnoreCase);
            bool showEndpoint = string.Equals(selectedType, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "AzureAIInference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "Mistral", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "HuggingFace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedType, "Ollama", StringComparison.OrdinalIgnoreCase);
            bool showDeployment = string.Equals(selectedType, "AzureOpenAI", StringComparison.OrdinalIgnoreCase);
            bool requiresApiKey = RequiresApiKeyForService(selectedType);
            bool showModerationToggle = string.Equals(selectedType, "OpenAI", StringComparison.OrdinalIgnoreCase);

            if (ViewModel.PasteAIConfiguration is not null)
            {
                ViewModel.PasteAIConfiguration.EndpointUrl = ViewModel.GetPasteAIEndpoint(selectedType);
            }

            PasteAIEndpointUrlTextBox.Visibility = showEndpoint ? Visibility.Visible : Visibility.Collapsed;
            PasteAIDeploymentNameTextBox.Visibility = showDeployment ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModelPanel.Visibility = isOnnx ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModerationToggle.Visibility = showModerationToggle ? Visibility.Visible : Visibility.Collapsed;
            PasteAIApiKeyPasswordBox.Visibility = requiresApiKey ? Visibility.Visible : Visibility.Collapsed;

            if (requiresApiKey)
            {
                PasteAIApiKeyPasswordBox.Password = ViewModel.GetPasteAIApiKey(selectedType);
            }
            else
            {
                PasteAIApiKeyPasswordBox.Password = string.Empty;
            }
        }

        private async void AdvancedAIProviderConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAdvancedAIUIVisibility();
            await AdvancedAIProviderConfigurationDialog.ShowAsync();
        }

        private void AdvancedAIProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string serviceType = AdvancedAIServiceTypeListView.SelectedValue?.ToString() ?? "OpenAI";
            string apiKey = AdvancedAIApiKeyPasswordBox.Password;
            string trimmedApiKey = apiKey?.Trim() ?? string.Empty;
            string endpoint = (ViewModel.AdvancedAIConfiguration.EndpointUrl ?? string.Empty).Trim();

            if (RequiresApiKeyForService(serviceType) && string.IsNullOrWhiteSpace(trimmedApiKey))
            {
                args.Cancel = true;
                return;
            }

            ViewModel.AdvancedAIConfiguration.EndpointUrl = endpoint;
            ViewModel.SaveAdvancedAICredential(serviceType, endpoint, trimmedApiKey);
            ViewModel.AdvancedAIConfiguration.EndpointUrl = ViewModel.GetAdvancedAIEndpoint(serviceType);
            AdvancedAIApiKeyPasswordBox.Password = ViewModel.GetAdvancedAIApiKey(serviceType);

            // Show success message
            ShowApiKeySavedMessage("Advanced AI");
        }

        private void PasteAIProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string serviceType = PasteAIServiceTypeListView.SelectedValue?.ToString() ?? "OpenAI";
            string apiKey = PasteAIApiKeyPasswordBox.Password;
            string trimmedApiKey = apiKey?.Trim() ?? string.Empty;
            string endpoint = (ViewModel.PasteAIConfiguration.EndpointUrl ?? string.Empty).Trim();

            if (RequiresApiKeyForService(serviceType) && string.IsNullOrWhiteSpace(trimmedApiKey))
            {
                args.Cancel = true;
                return;
            }

            ViewModel.PasteAIConfiguration.EndpointUrl = endpoint;
            ViewModel.SavePasteAICredential(serviceType, endpoint, trimmedApiKey);
            ViewModel.PasteAIConfiguration.EndpointUrl = ViewModel.GetPasteAIEndpoint(serviceType);
            PasteAIApiKeyPasswordBox.Password = ViewModel.GetPasteAIApiKey(serviceType);

            // Show success message
            ShowApiKeySavedMessage("Paste AI");
        }

        private async void PasteAIProviderConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatePasteAIUIVisibility();
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

        private static bool RequiresApiKeyForService(string serviceType)
        {
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                return true;
            }

            return serviceType.Equals("Onnx", StringComparison.OrdinalIgnoreCase)
                ? false
                : !serviceType.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                    && !serviceType.Equals("FoundryLocal", StringComparison.OrdinalIgnoreCase)
                    && !serviceType.Equals("WindowsML", StringComparison.OrdinalIgnoreCase)
                    && !serviceType.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
                    && !serviceType.Equals("AmazonBedrock", StringComparison.OrdinalIgnoreCase);
        }
    }
}
