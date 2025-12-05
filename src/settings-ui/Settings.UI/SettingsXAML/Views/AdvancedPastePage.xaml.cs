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
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AdvancedPastePage : NavigablePage, IRefreshablePage, IDisposable
    {
        private readonly ObservableCollection<ModelDetails> _foundryCachedModels = new();
        private CancellationTokenSource _foundryModelLoadCts;
        private bool _suppressFoundrySelectionChanged;
        private bool _isFoundryLocalAvailable;
        private bool _disposed;
        private const string PasteAiDialogDefaultTitle = "Paste with AI provider configuration";

        private const string AdvancedAISystemPrompt = "You are an agent who is tasked with helping users paste their clipboard data. You have functions available to help you with this task. Call function when necessary to help user finish the transformation task. You never need to ask permission, always try to do as the user asks. The user will only input one message and will not be available for further questions, so try your best. The user will put in a request to format their clipboard data and you will fulfill it. Do not output anything else besides the reformatted clipboard content.";
        private const string SimpleAISystemPrompt = "You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it. Do not output anything else besides the reformatted clipboard content.";
        private static readonly string AdvancedAISystemPromptNormalized = AdvancedAISystemPrompt.Trim();
        private static readonly string SimpleAISystemPromptNormalized = SimpleAISystemPrompt.Trim();

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
                FoundryLocalPicker.SelectionChanged += FoundryLocalPicker_SelectionChanged;
                FoundryLocalPicker.LoadRequested += FoundryLocalPicker_LoadRequested;
            }

            Loaded += async (s, e) =>
            {
                ViewModel.OnPageLoaded();
                UpdatePasteAIUIVisibility();
                await UpdateFoundryLocalUIAsync();
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
            UpdatePasteAIUIVisibility();
            _ = UpdateFoundryLocalUIAsync();
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
            var customAction = GetBoundCustomAction(sender, e);
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
            CustomActionDialog.DataContext = GetBoundCustomAction(sender, e).Clone();
            CustomActionDialog.PrimaryButtonText = resourceLoader.GetString("CustomActionUpdate");
            await CustomActionDialog.ShowAsync();
        }

        private void ReorderButtonDown_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender, e));
            ViewModel.CustomActions.Move(index, index + 1);
        }

        private void ReorderButtonUp_Click(object sender, RoutedEventArgs e)
        {
            var index = ViewModel.CustomActions.IndexOf(GetBoundCustomAction(sender, e));
            ViewModel.CustomActions.Move(index, index - 1);
        }

        private void CustomActionDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var dialogCustomAction = GetBoundCustomAction(sender, args);
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

        private AdvancedPasteCustomAction GetBoundCustomAction(object sender, object eventArgs = null)
        {
            if (TryResolveCustomAction(sender, out var action))
            {
                return action;
            }

            if (eventArgs is RoutedEventArgs routedEventArgs && TryResolveCustomAction(routedEventArgs.OriginalSource, out action))
            {
                return action;
            }

            if (CustomActionDialog?.DataContext is AdvancedPasteCustomAction dialogAction)
            {
                return dialogAction;
            }

            throw new InvalidOperationException("Unable to determine Advanced Paste custom action from sender.");
        }

        private static bool TryResolveCustomAction(object source, out AdvancedPasteCustomAction action)
        {
            action = ResolveCustomAction(source);
            return action is not null;
        }

        private static AdvancedPasteCustomAction ResolveCustomAction(object source)
        {
            if (source is null)
            {
                return null;
            }

            if (source is AdvancedPasteCustomAction directAction)
            {
                return directAction;
            }

            if (source is MenuFlyoutItemBase menuItem && menuItem.Tag is AdvancedPasteCustomAction taggedAction)
            {
                return taggedAction;
            }

            if (source is FrameworkElement element)
            {
                return ResolveFromElement(element);
            }

            return null;
        }

        private static AdvancedPasteCustomAction ResolveFromElement(FrameworkElement element)
        {
            for (FrameworkElement current = element; current is not null; current = VisualTreeHelper.GetParent(current) as FrameworkElement)
            {
                if (current.Tag is AdvancedPasteCustomAction tagged)
                {
                    return tagged;
                }

                if (current.DataContext is AdvancedPasteCustomAction contextual)
                {
                    return contextual;
                }
            }

            return null;
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
                if (ViewModel?.PasteAIProviderDraft is not null)
                {
                    ViewModel.PasteAIProviderDraft.ModelPath = selectedFile;
                }
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

        private void UpdatePasteAIUIVisibility()
        {
            var draft = ViewModel?.PasteAIProviderDraft;
            if (draft is null)
            {
                return;
            }

            string selectedType = draft.ServiceType ?? string.Empty;
            AIServiceType serviceKind = draft.ServiceTypeKind;

            bool requiresEndpoint = serviceKind is AIServiceType.AzureOpenAI
                or AIServiceType.AzureAIInference
                or AIServiceType.Mistral
                or AIServiceType.Ollama;
            bool requiresDeployment = serviceKind == AIServiceType.AzureOpenAI;
            bool requiresApiVersion = serviceKind == AIServiceType.AzureOpenAI;
            bool requiresModelPath = serviceKind == AIServiceType.Onnx;
            bool isFoundryLocal = serviceKind == AIServiceType.FoundryLocal;
            bool requiresApiKey = RequiresApiKeyForService(selectedType);
            bool showModerationToggle = serviceKind == AIServiceType.OpenAI;
            bool showAdvancedAI = serviceKind == AIServiceType.OpenAI || serviceKind == AIServiceType.AzureOpenAI;

            if (string.IsNullOrWhiteSpace(draft.EndpointUrl))
            {
                string storedEndpoint = ViewModel.GetPasteAIEndpoint(draft.Id, selectedType);
                if (!string.IsNullOrWhiteSpace(storedEndpoint))
                {
                    draft.EndpointUrl = storedEndpoint;
                }
            }

            PasteAIEndpointUrlTextBox.Visibility = requiresEndpoint ? Visibility.Visible : Visibility.Collapsed;
            if (requiresEndpoint)
            {
                PasteAIEndpointUrlTextBox.PlaceholderText = GetEndpointPlaceholder(serviceKind);
            }

            PasteAIDeploymentNameTextBox.Visibility = requiresDeployment ? Visibility.Visible : Visibility.Collapsed;
            PasteAIApiVersionTextBox.Visibility = requiresApiVersion ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModelPanel.Visibility = requiresModelPath ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModerationToggle.Visibility = showModerationToggle ? Visibility.Visible : Visibility.Collapsed;
            PasteAIEnableAdvancedAICheckBox.Visibility = showAdvancedAI ? Visibility.Visible : Visibility.Collapsed;
            PasteAIApiKeyPasswordBox.Visibility = requiresApiKey ? Visibility.Visible : Visibility.Collapsed;
            PasteAIModelNameTextBox.Visibility = isFoundryLocal ? Visibility.Collapsed : Visibility.Visible;

            if (requiresApiKey)
            {
                PasteAIApiKeyPasswordBox.Password = ViewModel.GetPasteAIApiKey(draft.Id, selectedType);
            }
            else
            {
                PasteAIApiKeyPasswordBox.Password = string.Empty;
            }

            // Update system prompt placeholder based on EnableAdvancedAI state
            UpdateSystemPromptPlaceholder();

            // Disable Save button if GPO blocks this provider
            if (PasteAIProviderConfigurationDialog is not null)
            {
                bool isAllowedByGPO = ViewModel?.IsServiceTypeAllowedByGPO(serviceKind) ?? true;

                if (!isAllowedByGPO)
                {
                    // GPO blocks this provider, disable save button
                    PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                }
                else if (isFoundryLocal)
                {
                    // For Foundry Local, UpdateFoundrySaveButtonState will handle button state
                    // based on model selection status
                }
                else
                {
                    // GPO allows this provider, enable save button
                    PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                }
            }
        }

        private Task UpdateFoundryLocalUIAsync()
        {
            string selectedType = ViewModel?.PasteAIProviderDraft?.ServiceType ?? string.Empty;
            bool isFoundryLocal = string.Equals(selectedType, "FoundryLocal", StringComparison.OrdinalIgnoreCase);

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

                if (PasteAIProviderConfigurationDialog is not null)
                {
                    PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                }

                return Task.CompletedTask;
            }

            if (PasteAIProviderConfigurationDialog is not null)
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
            }

            FoundryLocalPicker?.RequestLoad();

            return Task.CompletedTask;
        }

        private async Task LoadFoundryLocalModelsAsync()
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

                IEnumerable<ModelDetails> cachedModelsEnumerable = await provider.GetModelsAsync(cancelationToken: cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var cachedModels = cachedModelsEnumerable?.ToList() ?? new List<ModelDetails>();

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateFoundryCollections(cachedModels);
                    ShowFoundryAvailableState();
                    RestoreFoundrySelection(cachedModels);
                });
            }
            catch (OperationCanceledException)
            {
                // Loading cancelled; no action required.
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unable to load Foundry Local models. {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[AdvancedPastePage] Failed to load Foundry Local models: {ex}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowFoundryUnavailableState(errorMessage);
                });
            }
            finally
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateFoundrySaveButtonState();
                });
            }
        }

        private void ShowFoundryLoadingState()
        {
            _isFoundryLocalAvailable = false;

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.IsLoading = true;
                FoundryLocalPicker.IsAvailable = false;
                FoundryLocalPicker.StatusText = "Loading Foundry Local status...";
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
                FoundryLocalPicker.StatusText = message ?? "Foundry Local was not detected. Follow the CLI guide to install and start it.";
            }

            _foundryCachedModels.Clear();
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
                    FoundryLocalPicker.StatusText = "No local models detected. Use the button below to list models and download them with Foundry Local.";
                }
                else if (string.IsNullOrWhiteSpace(FoundryLocalPicker.StatusText))
                {
                    FoundryLocalPicker.StatusText = "Select a downloaded model from the list to enable Advanced Paste.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private void UpdateFoundryCollections(IReadOnlyCollection<ModelDetails> cachedModels)
        {
            _foundryCachedModels.Clear();

            foreach (var model in cachedModels.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                _foundryCachedModels.Add(model);
            }

            var cachedReferences = new HashSet<string>(_foundryCachedModels.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
        }

        private void RestoreFoundrySelection(IReadOnlyCollection<ModelDetails> cachedModels)
        {
            if (FoundryLocalPicker is null)
            {
                return;
            }

            var currentModelReference = ViewModel?.PasteAIProviderDraft?.ModelName;

            ModelDetails matchingModel = null;

            if (!string.IsNullOrWhiteSpace(currentModelReference))
            {
                matchingModel = cachedModels.FirstOrDefault(model =>
                    string.Equals(model.Name, currentModelReference, StringComparison.OrdinalIgnoreCase));
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
                if (ViewModel?.PasteAIProviderDraft is not null)
                {
                    ViewModel.PasteAIProviderDraft.ModelName = string.Empty;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = _foundryCachedModels.Count == 0
                        ? "No local models detected. Use the button below to list models and download them with Foundry Local."
                        : "Select a downloaded model from the list to enable Advanced Paste.";
                }
            }
            else
            {
                if (ViewModel?.PasteAIProviderDraft is not null)
                {
                    ViewModel.PasteAIProviderDraft.ModelName = matchingModel.Name;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"{matchingModel.Name} selected.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private void UpdateFoundrySaveButtonState()
        {
            if (PasteAIProviderConfigurationDialog is null)
            {
                return;
            }

            bool isFoundrySelected = string.Equals(ViewModel?.PasteAIProviderDraft?.ServiceType, "FoundryLocal", StringComparison.OrdinalIgnoreCase);

            if (!isFoundrySelected || ViewModel?.PasteAIProviderDraft is null)
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                return;
            }

            // Check GPO first
            bool isAllowedByGPO = ViewModel?.IsServiceTypeAllowedByGPO(AIServiceType.FoundryLocal) ?? true;
            if (!isAllowedByGPO)
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            if (!_isFoundryLocalAvailable)
            {
                PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            bool hasSelection = FoundryLocalPicker?.SelectedModel is ModelDetails;
            PasteAIProviderConfigurationDialog.IsPrimaryButtonEnabled = hasSelection;
        }

        private void FoundryLocalPicker_SelectionChanged(object sender, ModelDetails selectedModel)
        {
            if (_suppressFoundrySelectionChanged)
            {
                return;
            }

            if (selectedModel is not null)
            {
                if (ViewModel?.PasteAIProviderDraft is not null)
                {
                    ViewModel.PasteAIProviderDraft.ModelName = selectedModel.Name;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"{selectedModel.Name} selected.";
                }
            }
            else
            {
                if (ViewModel?.PasteAIProviderDraft is not null)
                {
                    ViewModel.PasteAIProviderDraft.ModelName = string.Empty;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = "Select a downloaded model from the list to enable Advanced Paste.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private async void FoundryLocalPicker_LoadRequested(object sender)
        {
            await LoadFoundryLocalModelsAsync();
        }

        private sealed class FoundryDownloadableModel : INotifyPropertyChanged
        {
            private readonly List<string> _deviceTags;
            private double _progress;
            private bool _isDownloading;
            private bool _isDownloaded;

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

        private void PasteAIProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var draft = ViewModel?.PasteAIProviderDraft;
            if (draft is null)
            {
                args.Cancel = true;
                return;
            }

            NormalizeSystemPrompt(draft);
            string serviceType = draft.ServiceType ?? "OpenAI";
            string apiKey = PasteAIApiKeyPasswordBox.Password;
            string trimmedApiKey = apiKey?.Trim() ?? string.Empty;
            string endpoint = (draft.EndpointUrl ?? string.Empty).Trim();
            if (endpoint == string.Empty)
            {
                endpoint = GetEndpointPlaceholder(draft.ServiceTypeKind);
            }

            if (RequiresApiKeyForService(serviceType) && string.IsNullOrWhiteSpace(trimmedApiKey))
            {
                args.Cancel = true;
                return;
            }

            ViewModel.CommitPasteAIProviderDraft(trimmedApiKey, endpoint);
            PasteAIApiKeyPasswordBox.Password = string.Empty;

            // Show success message
            ShowApiKeySavedMessage("Paste AI");
        }

        private void PasteAIEnableAdvancedAICheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            var draft = ViewModel?.PasteAIProviderDraft;
            if (draft is null)
            {
                return;
            }

            NormalizeSystemPrompt(draft);
            UpdateSystemPromptPlaceholder();
        }

        private static bool RequiresApiKeyForService(string serviceType)
        {
            var serviceKind = serviceType.ToAIServiceType();

            return serviceKind switch
            {
                AIServiceType.Onnx => false,
                AIServiceType.Ollama => false,
                AIServiceType.FoundryLocal => false,
                AIServiceType.ML => false,
                _ => true,
            };
        }

        private static string GetEndpointPlaceholder(AIServiceType serviceKind)
        {
            return serviceKind switch
            {
                AIServiceType.AzureOpenAI => "https://your-resource.openai.azure.com/",
                AIServiceType.AzureAIInference => "https://{resource-name}.cognitiveservices.azure.com/",
                AIServiceType.Mistral => "https://api.mistral.ai/v1/",
                AIServiceType.Ollama => "http://localhost:11434/",
                _ => "https://your-resource.openai.azure.com/",
            };
        }

        private bool HasServiceLegalInfo(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return metadata.HasLegalInfo;
        }

        private string GetServiceLegalDescription(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            if (string.IsNullOrWhiteSpace(metadata.LegalDescription))
            {
                return string.Empty;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            return resourceLoader.GetString(metadata.LegalDescription);
        }

        private string GetServiceTermsLabel(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            if (string.IsNullOrWhiteSpace(metadata.TermsLabel))
            {
                return string.Empty;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            return resourceLoader.GetString(metadata.TermsLabel);
        }

        private Uri GetServiceTermsUri(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return metadata.TermsUri;
        }

        private string GetServicePrivacyLabel(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            if (string.IsNullOrWhiteSpace(metadata.PrivacyLabel))
            {
                return string.Empty;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            return resourceLoader.GetString(metadata.PrivacyLabel);
        }

        private Uri GetServicePrivacyUri(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return metadata.PrivacyUri;
        }

        private bool HasServiceTermsLink(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return metadata.HasTermsLink;
        }

        private bool HasServicePrivacyLink(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return metadata.HasPrivacyLink;
        }

        private Visibility GetServiceLegalVisibility(string serviceType) => HasServiceLegalInfo(serviceType) ? Visibility.Visible : Visibility.Collapsed;

        private Visibility GetServiceTermsVisibility(string serviceType) => HasServiceTermsLink(serviceType) ? Visibility.Visible : Visibility.Collapsed;

        private Visibility GetServicePrivacyVisibility(string serviceType) => HasServicePrivacyLink(serviceType) ? Visibility.Visible : Visibility.Collapsed;

        private static bool IsPlaceholderSystemPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return true;
            }

            string trimmedPrompt = prompt.Trim();
            return string.Equals(trimmedPrompt, AdvancedAISystemPromptNormalized, StringComparison.Ordinal)
                || string.Equals(trimmedPrompt, SimpleAISystemPromptNormalized, StringComparison.Ordinal);
        }

        private static void NormalizeSystemPrompt(PasteAIProviderDefinition draft)
        {
            if (draft is null)
            {
                return;
            }

            if (IsPlaceholderSystemPrompt(draft.SystemPrompt))
            {
                draft.SystemPrompt = string.Empty;
            }
        }

        private void UpdateSystemPromptPlaceholder()
        {
            var draft = ViewModel?.PasteAIProviderDraft;
            if (draft is null)
            {
                return;
            }

            NormalizeSystemPrompt(draft);
            if (PasteAISystemPromptTextBox is null)
            {
                return;
            }

            bool useAdvancedPlaceholder = PasteAIEnableAdvancedAICheckBox?.IsOn ?? draft.EnableAdvancedAI;
            PasteAISystemPromptTextBox.PlaceholderText = useAdvancedPlaceholder
                ? AdvancedAISystemPrompt
                : SimpleAISystemPrompt;
        }

        private void RefreshDialogBindings()
        {
            try
            {
                Bindings?.Update();
            }
            catch (Exception)
            {
                // Best-effort refresh only; ignore refresh failures.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _foundryModelLoadCts?.Cancel();
            }
            catch (Exception)
            {
                // Ignore cancellation failures during disposal.
            }

            _foundryModelLoadCts?.Dispose();
            _foundryModelLoadCts = null;

            if (FoundryLocalPicker is not null)
            {
                FoundryLocalPicker.SelectionChanged -= FoundryLocalPicker_SelectionChanged;
                FoundryLocalPicker.LoadRequested -= FoundryLocalPicker_LoadRequested;
            }

            ViewModel?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void AddProviderMenuFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout menuFlyout)
            {
                return;
            }

            // Clear existing items
            menuFlyout.Items.Clear();

            // Add online models header
            var onlineHeader = new MenuFlyoutItem
            {
                Text = "Online models",
                FontSize = 12,
                IsEnabled = false,
                IsHitTestVisible = false,
            };
            menuFlyout.Items.Add(onlineHeader);

            // Add all online providers
            var onlineProviders = AIServiceTypeRegistry.GetOnlineServiceTypes();

            foreach (var metadata in onlineProviders)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = metadata.DisplayName,
                    Tag = metadata.ServiceType.ToConfigurationString(),
                    Icon = new ImageIcon { Source = new SvgImageSource(new Uri(metadata.IconPath)) },
                };

                menuItem.Click += ProviderMenuFlyoutItem_Click;
                menuFlyout.Items.Add(menuItem);
            }

            // Add local models header
            var localHeader = new MenuFlyoutItem
            {
                Text = "Local models",
                FontSize = 12,
                IsEnabled = false,
                IsHitTestVisible = false,
                Margin = new Thickness(0, 16, 0, 0),
            };
            menuFlyout.Items.Add(localHeader);

            // Add all local providers
            var localProviders = AIServiceTypeRegistry.GetLocalServiceTypes();

            foreach (var metadata in localProviders)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = metadata.DisplayName,
                    Tag = metadata.ServiceType.ToConfigurationString(),
                    Icon = new ImageIcon { Source = new SvgImageSource(new Uri(metadata.IconPath)) },
                };

                menuItem.Click += ProviderMenuFlyoutItem_Click;
                menuFlyout.Items.Add(menuItem);
            }
        }

        private async void ProviderMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not string tag || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (ViewModel is null || PasteAIProviderConfigurationDialog is null)
            {
                return;
            }

            string serviceType = tag.Trim();
            string displayName = string.IsNullOrWhiteSpace(menuItem.Text) ? serviceType : menuItem.Text.Trim();

            ViewModel.BeginAddPasteAIProvider(serviceType);
            if (ViewModel.PasteAIProviderDraft is null)
            {
                return;
            }

            PasteAIProviderConfigurationDialog.Title = PasteAiDialogDefaultTitle;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                PasteAIProviderConfigurationDialog.Title = $"{displayName} provider configuration";
            }

            await UpdateFoundryLocalUIAsync();
            UpdatePasteAIUIVisibility();
            RefreshDialogBindings();

            PasteAIApiKeyPasswordBox.Password = string.Empty;
            await PasteAIProviderConfigurationDialog.ShowAsync();
        }

        private async void EditPasteAIProviderButton_Click(object sender, RoutedEventArgs e)
        {
            // sender is MenuFlyoutItem with PasteAIProviderDefinition Tag
            if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not PasteAIProviderDefinition provider)
            {
                return;
            }

            if (ViewModel is null || PasteAIProviderConfigurationDialog is null)
            {
                return;
            }

            ViewModel.BeginEditPasteAIProvider(provider);

            string titlePrefix = string.IsNullOrWhiteSpace(provider.ModelName) ? provider.ServiceType : provider.ModelName;
            PasteAIProviderConfigurationDialog.Title = string.IsNullOrWhiteSpace(titlePrefix)
                ? PasteAiDialogDefaultTitle
                : $"{titlePrefix} provider configuration";

            UpdatePasteAIUIVisibility();
            await UpdateFoundryLocalUIAsync();
            RefreshDialogBindings();
            PasteAIApiKeyPasswordBox.Password = ViewModel.GetPasteAIApiKey(provider.Id, provider.ServiceType);
            await PasteAIProviderConfigurationDialog.ShowAsync();
        }

        private void RemovePasteAIProviderButton_Click(object sender, RoutedEventArgs e)
        {
            // sender is MenuFlyoutItem with PasteAIProviderDefinition Tag
            if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not PasteAIProviderDefinition provider)
            {
                return;
            }

            ViewModel?.RemovePasteAIProvider(provider);
        }

        private void PasteAIProviderConfigurationDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel?.CancelPasteAIProviderDraft();
            PasteAIProviderConfigurationDialog.Title = PasteAiDialogDefaultTitle;
            PasteAIApiKeyPasswordBox.Password = string.Empty;
        }
    }
}
