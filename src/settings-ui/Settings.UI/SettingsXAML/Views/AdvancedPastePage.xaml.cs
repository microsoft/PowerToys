// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using LanguageModelProvider;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : NavigablePage, IRefreshablePage, IDisposable
    {
        private readonly ObservableCollection<ModelDetails> _foundryCachedModels = new();
        private readonly ObservableCollection<FoundryDownloadableModel> _foundryDownloadableModels = new();
        private CancellationTokenSource _foundryModelLoadCts;
        private bool _suppressFoundrySelectionChanged;
        private bool _isFoundryLocalAvailable;

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

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.CachedModels = _foundryCachedModels;
                FoundryLocalPicker.DownloadableModels = _foundryDownloadableModels;
                FoundryLocalPicker.SelectionChanged += FoundryLocalPicker_SelectionChanged;
                FoundryLocalPicker.DownloadRequested += FoundryLocalPicker_DownloadRequested;
            }

            Loaded += async (s, e) =>
            {
                ViewModel.OnPageLoaded();
                UpdateAdvancedAIUIVisibility();
                await UpdatePasteAIUIVisibilityAsync();
            };

            Unloaded += (_, _) =>
            {
                if (_foundryModelLoadCts is not null)
                {
                    _foundryModelLoadCts.Cancel();
                    _foundryModelLoadCts.Dispose();
                    _foundryModelLoadCts = null;
                }
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
                ViewModel.AddCustomAction(dialogCustomAction);
            }
            else
            {
                existingCustomAction.Update(dialogCustomAction);
            }
        }

        private static AdvancedPasteCustomAction GetBoundCustomAction(object sender)
        {
            if (sender is FrameworkElement element)
            {
                if (element.DataContext is AdvancedPasteCustomAction action)
                {
                    return action;
                }

                if (element.Tag is AdvancedPasteCustomAction taggedAction)
                {
                    return taggedAction;
                }
            }

            throw new InvalidOperationException("Unable to determine Advanced Paste custom action from sender.");
        }

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

        private async void PasteAIServiceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdatePasteAIUIVisibilityAsync();
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

        private async Task UpdatePasteAIUIVisibilityAsync(bool refreshFoundry = false)
        {
            if (PasteAIServiceTypeListView?.SelectedValue == null)
            {
                return;
            }

            string selectedType = PasteAIServiceTypeListView.SelectedValue.ToString();
            bool isAzureOpenAI = string.Equals(selectedType, "AzureOpenAI", StringComparison.Ordinal);
            bool isOnnx = string.Equals(selectedType, "Onnx", StringComparison.Ordinal);
            bool isFoundryLocal = string.Equals(selectedType, "FoundryLocal", StringComparison.Ordinal);

            PasteAIModelNameTextBox.Visibility = isFoundryLocal ? Visibility.Collapsed : Visibility.Visible;
            PasteAIEndpointUrlTextBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
            PasteAIDeploymentNameTextBox.Visibility = isAzureOpenAI ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModelPanel.Visibility = isOnnx ? Visibility.Visible : Visibility.Collapsed;
            PasteAIApiKeyPasswordBox.Visibility = (!isOnnx && !isFoundryLocal) ? Visibility.Visible : Visibility.Collapsed;

            if (FoundryLocalPanel is not null)
            {
                FoundryLocalPanel.Visibility = isFoundryLocal ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!isFoundryLocal)
            {
                _foundryModelLoadCts?.Cancel();
                _isFoundryLocalAvailable = false;
                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.IsLoading = false;
                    FoundryLocalPicker.IsAvailable = false;
                    FoundryLocalPicker.StatusText = string.Empty;
                    FoundryLocalPicker.SelectedModel = null;
                }

                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                return;
            }

            PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;

            await LoadFoundryLocalModelsAsync(refreshFoundry);
        }

        private async Task LoadFoundryLocalModelsAsync(bool refresh = false)
        {
            if (FoundryLocalPanel is null)
            {
                return;
            }

            _foundryModelLoadCts?.Cancel();
            _foundryModelLoadCts?.Dispose();
            _foundryModelLoadCts = new CancellationTokenSource();
            var cancellationToken = _foundryModelLoadCts.Token;

            ShowFoundryLoadingState();

            try
            {
                var provider = FoundryLocalModelProvider.Instance;

                var isAvailable = await provider.IsAvailable();
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _isFoundryLocalAvailable = isAvailable;

                if (!isAvailable)
                {
                    ShowFoundryUnavailableState();
                    return;
                }

                IEnumerable<ModelDetails> cachedModelsEnumerable = refresh
                    ? await provider.GetModelsAsync(ignoreCached: true, cancelationToken: cancellationToken)
                    : await provider.GetModelsAsync(cancelationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var cachedModels = cachedModelsEnumerable?.ToList() ?? new List<ModelDetails>();
                var catalogModels = provider.GetAllModelsInCatalog()?.ToList() ?? new List<ModelDetails>();

                UpdateFoundryCollections(cachedModels, catalogModels);
                ShowFoundryAvailableState();
                RestoreFoundrySelection(cachedModels);
            }
            catch (OperationCanceledException)
            {
                // Loading cancelled; no action required.
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unable to load Foundry Local models. {ex.Message}";
                ShowFoundryUnavailableState(errorMessage);
                System.Diagnostics.Debug.WriteLine($"[AdvancedPastePage] Failed to load Foundry Local models: {ex}");
            }
            finally
            {
                UpdateFoundrySaveButtonState();
            }
        }

        private void ShowFoundryLoadingState()
        {
            _isFoundryLocalAvailable = false;

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.IsLoading = true;
                FoundryLocalPicker.IsAvailable = false;
                FoundryLocalPicker.StatusText = string.Empty;
                FoundryLocalPicker.SelectedModel = null;
            }
        }

        private void ShowFoundryUnavailableState(string message = null)
        {
            _isFoundryLocalAvailable = false;

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.IsLoading = false;
                FoundryLocalPicker.IsAvailable = false;
                FoundryLocalPicker.SelectedModel = null;
                FoundryLocalPicker.StatusText = message ?? "Foundry Local was not detected. Install it to use local models.";
            }

            _foundryCachedModels.Clear();
            _foundryDownloadableModels.Clear();
        }

        private void ShowFoundryAvailableState()
        {
            _isFoundryLocalAvailable = true;

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.IsLoading = false;
                FoundryLocalPicker.IsAvailable = true;
                if (_foundryCachedModels.Count == 0)
                {
                    FoundryLocalPicker.StatusText = "Download a model to enable Advanced Paste.";
                }
                else if (string.IsNullOrWhiteSpace(FoundryLocalPicker.StatusText))
                {
                    FoundryLocalPicker.StatusText = "Select a downloaded model to enable Advanced Paste.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private void UpdateFoundryCollections(IReadOnlyCollection<ModelDetails> cachedModels, IReadOnlyCollection<ModelDetails> catalogModels)
        {
            _foundryCachedModels.Clear();

            foreach (var model in cachedModels.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                _foundryCachedModels.Add(model);
            }

            var cachedReferences = new HashSet<string>(_foundryCachedModels.Select(m => NormalizeFoundryModelReference(m.Url ?? m.Name)), StringComparer.OrdinalIgnoreCase);

            _foundryDownloadableModels.Clear();

            foreach (var model in catalogModels.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                var reference = NormalizeFoundryModelReference(model.Url ?? model.Name);
                if (cachedReferences.Contains(reference))
                {
                    continue;
                }

                _foundryDownloadableModels.Add(new FoundryDownloadableModel(model));
            }
        }

        private void RestoreFoundrySelection(IReadOnlyCollection<ModelDetails> cachedModels)
        {
            if (FoundryLocalPicker is null)
            {
                return;
            }

            var currentModelReference = ViewModel?.PasteAIConfiguration?.ModelName;

            ModelDetails matchingModel = null;

            if (!string.IsNullOrWhiteSpace(currentModelReference))
            {
                var normalizedReference = NormalizeFoundryModelReference(currentModelReference);
                matchingModel = cachedModels.FirstOrDefault(model =>
                    string.Equals(NormalizeFoundryModelReference(model.Url ?? model.Name), normalizedReference, StringComparison.OrdinalIgnoreCase));
            }

            if (FoundryLocalPicker is null)
            {
                return;
            }

            _suppressFoundrySelectionChanged = true;
            FoundryLocalPicker.SelectedModel = matchingModel;
            _suppressFoundrySelectionChanged = false;

            if (matchingModel is null)
            {
                if (ViewModel?.PasteAIConfiguration is not null)
                {
                    ViewModel.PasteAIConfiguration.ModelName = string.Empty;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = _foundryCachedModels.Count == 0
                        ? "Download a model to enable Advanced Paste."
                        : "Select a downloaded model to enable Advanced Paste.";
                }
            }
            else
            {
                if (ViewModel?.PasteAIConfiguration is not null)
                {
                    ViewModel.PasteAIConfiguration.ModelName = NormalizeFoundryModelReference(matchingModel.Url ?? matchingModel.Name);
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"{matchingModel.Name} selected.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private static string NormalizeFoundryModelReference(string modelReference)
        {
            if (string.IsNullOrWhiteSpace(modelReference))
            {
                return string.Empty;
            }

            var prefix = FoundryLocalModelProvider.Instance.UrlPrefix;
            return modelReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? modelReference
                : $"{prefix}{modelReference}";
        }

        private async Task DownloadFoundryModelAsync(FoundryDownloadableModel downloadableModel)
        {
            if (downloadableModel is null)
            {
                return;
            }

            downloadableModel.StartDownload();
            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.StatusText = $"Downloading {downloadableModel.Name}...";
            }

            UpdateFoundrySaveButtonState();

            try
            {
                var provider = FoundryLocalModelProvider.Instance;
                var progress = new Progress<float>(value => downloadableModel.ReportProgress(value));

                bool success = await provider.DownloadModel(downloadableModel.ModelDetails, progress);

                if (success)
                {
                    downloadableModel.MarkDownloaded();

                    if (FoundryLocalPicker is not null)
                    {
                        FoundryLocalPicker.StatusText = $"Downloaded {downloadableModel.Name}.";
                    }

                    if (ViewModel?.PasteAIConfiguration is not null)
                    {
                        ViewModel.PasteAIConfiguration.ModelName = NormalizeFoundryModelReference(downloadableModel.ModelDetails.Url ?? downloadableModel.ModelDetails.Name);
                    }

                    await LoadFoundryLocalModelsAsync(refresh: true);
                }
                else
                {
                    downloadableModel.Reset();
                    if (FoundryLocalPicker is not null)
                    {
                        FoundryLocalPicker.StatusText = $"Failed to download {downloadableModel.Name}.";
                    }
                }
            }
            catch (Exception ex)
            {
                downloadableModel.Reset();
                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"Failed to download {downloadableModel.Name}.";
                }

                System.Diagnostics.Debug.WriteLine($"[AdvancedPastePage] Failed to download Foundry Local model: {ex}");
            }
            finally
            {
                UpdateFoundrySaveButtonState();
            }
        }

        private void UpdateFoundrySaveButtonState()
        {
            if (PasteAIProviderConfigurationDialog is null)
            {
                return;
            }

            bool isFoundrySelected = string.Equals(PasteAIServiceTypeListView?.SelectedValue?.ToString(), "FoundryLocal", StringComparison.Ordinal);

            if (!isFoundrySelected)
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                return;
            }

            if (!_isFoundryLocalAvailable || _foundryDownloadableModels.Any(model => model.IsDownloading))
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            bool hasSelection = FoundryLocalPicker?.SelectedModel is ModelDetails;
            PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = hasSelection;
        }

        private void FoundryLocalPicker_SelectionChanged(object sender, ModelDetails? selectedModel)
        {
            if (_suppressFoundrySelectionChanged)
            {
                return;
            }

            if (selectedModel is not null)
            {
                if (ViewModel?.PasteAIConfiguration is not null)
                {
                    ViewModel.PasteAIConfiguration.ModelName = NormalizeFoundryModelReference(selectedModel.Url ?? selectedModel.Name);
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"{selectedModel.Name} selected.";
                }
            }
            else
            {
                if (ViewModel?.PasteAIConfiguration is not null)
                {
                    ViewModel.PasteAIConfiguration.ModelName = string.Empty;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = "Select a downloaded model to enable Advanced Paste.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private async void FoundryLocalPicker_DownloadRequested(object sender, object? args)
        {
            if (args is FoundryDownloadableModel downloadableModel)
            {
                await DownloadFoundryModelAsync(downloadableModel);
            }
        }

        private sealed class FoundryDownloadableModel : INotifyPropertyChanged
        {
            private double _progress;
            private bool _isDownloading;
            private bool _isDownloaded;
            private readonly List<string> _deviceTags;

            public FoundryDownloadableModel(ModelDetails modelDetails)
            {
                ModelDetails = modelDetails ?? throw new ArgumentNullException(nameof(modelDetails));
                SizeTag = FoundryLocalModelPicker.GetModelSizeText(ModelDetails.Size);
                LicenseTag = FoundryLocalModelPicker.GetLicenseShortText(ModelDetails.License);
                _deviceTags = FoundryLocalModelPicker
                    .GetDeviceTags(ModelDetails.HardwareAccelerators)
                    .ToList();
            }

            public ModelDetails ModelDetails { get; }

            public string Name => string.IsNullOrWhiteSpace(ModelDetails.Name) ? "Model" : ModelDetails.Name;

            public string Description => string.IsNullOrWhiteSpace(ModelDetails.Description) ? "No description provided." : ModelDetails.Description;

            public string SizeTag { get; }

            public bool HasSizeTag => !string.IsNullOrWhiteSpace(SizeTag);

            public string LicenseTag { get; }

            public bool HasLicenseTag => !string.IsNullOrWhiteSpace(LicenseTag);

            public IReadOnlyList<string> DeviceTags => _deviceTags;

            public bool HasDeviceTags => _deviceTags.Count > 0;

            public double ProgressPercent => Math.Round(_progress * 100, 2);

            public Visibility ProgressVisibility => _isDownloading ? Visibility.Visible : Visibility.Collapsed;

            public string ActionLabel => _isDownloaded ? "Downloaded" : _isDownloading ? "Downloading..." : "Download";

            public bool CanDownload => !_isDownloading && !_isDownloaded;

            internal bool IsDownloading => _isDownloading;

            public event PropertyChangedEventHandler PropertyChanged;

            public void StartDownload()
            {
                _isDownloading = true;
                _isDownloaded = false;
                _progress = 0;
                NotifyStateChanged();
            }

            public void ReportProgress(float value)
            {
                _progress = Math.Clamp(value, 0f, 1f);
                RaisePropertyChanged(nameof(ProgressPercent));
            }

            public void MarkDownloaded()
            {
                _isDownloading = false;
                _isDownloaded = true;
                _progress = 1;
                NotifyStateChanged();
            }

            public void Reset()
            {
                _isDownloading = false;
                _isDownloaded = false;
                _progress = 0;
                NotifyStateChanged();
            }

            private void NotifyStateChanged()
            {
                RaisePropertyChanged(nameof(ProgressPercent));
                RaisePropertyChanged(nameof(ProgressVisibility));
                RaisePropertyChanged(nameof(ActionLabel));
                RaisePropertyChanged(nameof(CanDownload));
            }

            private void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private async void PasteAIServiceTypeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdatePasteAIUIVisibilityAsync();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
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
