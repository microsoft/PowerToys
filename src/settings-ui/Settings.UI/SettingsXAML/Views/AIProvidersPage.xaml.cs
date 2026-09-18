// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using LanguageModelProvider;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AIProvidersPage : NavigablePage, IRefreshablePage, IDisposable
    {
        private readonly ObservableCollection<ModelDetails> _foundryCachedModels = new();
        private CancellationTokenSource _foundryModelLoadCts;
        private bool _suppressFoundrySelectionChanged;
        private bool _isFoundryLocalAvailable;
        private bool _isPhiSilicaAvailable;
        private bool _disposed;
        private const string DialogDefaultTitle = "AI provider configuration";

        private const string AdvancedAISystemPrompt = "You are an agent who is tasked with helping users paste their clipboard data. You have functions available to help you with this task. Call function when necessary to help user finish the transformation task. You never need to ask permission, always try to do as the user asks. The user will only input one message and will not be available for further questions, so try your best. The user will put in a request to format their clipboard data and you will fulfill it. Do not output anything else besides the reformatted clipboard content.";
        private const string SimpleAISystemPrompt = "You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it. Do not output anything else besides the reformatted clipboard content.";
        private static readonly string AdvancedAISystemPromptNormalized = AdvancedAISystemPrompt.Trim();
        private static readonly string SimpleAISystemPromptNormalized = SimpleAISystemPrompt.Trim();
        private static readonly char[] NewLineSeparators = ['\r', '\n'];

        private AIProvidersViewModel ViewModel { get; set; }

        public AIProvidersPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new AIProvidersViewModel(settingsUtils, ShellPage.SendDefaultIPCMessage);
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
                ViewModel.ReloadFromDisk();
                UpdateProviderUIVisibility();
                await UpdateFoundryLocalUIAsync();
                await UpdatePhiSilicaUIAsync();
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
            UpdateProviderUIVisibility();
            _ = UpdateFoundryLocalUIAsync();
            _ = UpdatePhiSilicaUIAsync();
        }

        private void BrowseModelPath_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = PickFileDialog(
                "ONNX Model Files\0*.onnx\0All Files\0*.*\0",
                "Select ONNX Model File");

            if (!string.IsNullOrEmpty(selectedFile))
            {
                ModelPathTextBox.Text = selectedFile;
                if (ViewModel?.ProviderDraft is not null)
                {
                    ViewModel.ProviderDraft.ModelPath = selectedFile;
                }
            }
        }

        private static string PickFileDialog(string filter, string title, string initialDir = null, int initialFilter = 0)
        {
            OpenFileName openFileName = new OpenFileName();
            openFileName.StructSize = Marshal.SizeOf(openFileName);
            openFileName.Filter = filter;
            openFileName.File = new string(new char[260 * 2]);
            openFileName.MaxFile = openFileName.File.Length;
            openFileName.FileTitle = new string(new char[260 * 2]);
            openFileName.MaxFileTitle = openFileName.FileTitle.Length;
            openFileName.InitialDir = initialDir;
            openFileName.Title = title;
            openFileName.FilterIndex = initialFilter;
            openFileName.DefExt = null;
            openFileName.Flags = (int)OpenFileNameFlags.OFN_NOCHANGEDIR;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            openFileName.Hwnd = windowHandle;

            return NativeMethods.GetOpenFileName(openFileName) ? openFileName.File : null;
        }

        private void UpdateProviderUIVisibility()
        {
            var draft = ViewModel?.ProviderDraft;
            if (draft is null)
            {
                return;
            }

            string selectedType = draft.ServiceType ?? string.Empty;
            AIServiceType serviceKind = draft.ServiceTypeKind;

            bool requiresEndpoint = RequiresEndpointForService(serviceKind);
            bool requiresDeployment = serviceKind == AIServiceType.AzureOpenAI;
            bool requiresApiVersion = serviceKind == AIServiceType.AzureOpenAI;
            bool requiresModelPath = serviceKind == AIServiceType.Onnx;
            bool isFoundryLocal = serviceKind == AIServiceType.FoundryLocal;
            bool isPhiSilica = serviceKind == AIServiceType.PhiSilica;
            bool requiresApiKey = RequiresApiKeyForService(selectedType);
            bool requiresModelName = !isFoundryLocal && !isPhiSilica;
            bool showModerationToggle = serviceKind == AIServiceType.OpenAI;
            bool showAdvancedAI = serviceKind == AIServiceType.OpenAI || serviceKind == AIServiceType.AzureOpenAI;

            if (string.IsNullOrWhiteSpace(draft.EndpointUrl))
            {
                string storedEndpoint = ViewModel.GetProviderEndpoint(draft.Id, selectedType);
                if (!string.IsNullOrWhiteSpace(storedEndpoint))
                {
                    draft.EndpointUrl = storedEndpoint;
                }
            }

            EndpointUrlTextBox.Visibility = requiresEndpoint ? Visibility.Visible : Visibility.Collapsed;
            if (requiresEndpoint)
            {
                EndpointUrlTextBox.PlaceholderText = GetEndpointPlaceholder(serviceKind);
            }

            DeploymentNameTextBox.Visibility = requiresDeployment ? Visibility.Visible : Visibility.Collapsed;
            ApiVersionTextBox.Visibility = requiresApiVersion ? Visibility.Visible : Visibility.Collapsed;
            ModelPathPanel.Visibility = requiresModelPath ? Visibility.Visible : Visibility.Collapsed;
            ModerationToggle.Visibility = showModerationToggle ? Visibility.Visible : Visibility.Collapsed;
            EnableAdvancedAICheckBox.Visibility = showAdvancedAI ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyPasswordBox.Visibility = requiresApiKey ? Visibility.Visible : Visibility.Collapsed;
            ModelNameTextBox.Visibility = requiresModelName ? Visibility.Visible : Visibility.Collapsed;

            ApiKeyPasswordBox.Password = requiresApiKey
                ? ViewModel.GetApiKey(draft.Id, selectedType)
                : string.Empty;

            UpdateSystemPromptPlaceholder();

            if (ProviderConfigurationDialog is not null)
            {
                bool isAllowedByGPO = ViewModel?.IsServiceTypeAllowedByGPO(serviceKind) ?? true;

                if (!isAllowedByGPO)
                {
                    ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                }
                else if (!isFoundryLocal && !isPhiSilica)
                {
                    ProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                }
            }
        }

        private Task UpdateFoundryLocalUIAsync()
        {
            string selectedType = ViewModel?.ProviderDraft?.ServiceType ?? string.Empty;
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

                if (ProviderConfigurationDialog is not null)
                {
                    ProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                }

                return Task.CompletedTask;
            }

            if (ProviderConfigurationDialog is not null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
            }

            FoundryLocalPicker?.RequestLoad();
            return Task.CompletedTask;
        }

        private async Task UpdatePhiSilicaUIAsync()
        {
            string selectedType = ViewModel?.ProviderDraft?.ServiceType ?? string.Empty;
            bool isPhiSilica = string.Equals(selectedType, "PhiSilica", StringComparison.OrdinalIgnoreCase);

            if (PhiSilicaPanel is not null)
            {
                PhiSilicaPanel.Visibility = isPhiSilica ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!isPhiSilica)
            {
                _isPhiSilicaAvailable = false;
                return;
            }

            if (ProviderConfigurationDialog is not null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
            }

            ShowPhiSilicaLoadingState();
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            try
            {
                var (status, diagnostics) = await Task.Run(() => CheckPhiSilicaViaAdvancedPaste());

                if (status == "NotSupported")
                {
                    _isPhiSilicaAvailable = false;
                    ShowPhiSilicaNotAvailableState(
                        resourceLoader.GetString("AdvancedPaste_PhiSilicaNotAvailable_Title"),
                        resourceLoader.GetString("AdvancedPaste_PhiSilicaNotAvailable_Description"),
                        details: diagnostics);
                }
                else if (status == "NotReady")
                {
                    _isPhiSilicaAvailable = false;
                    ShowPhiSilicaNotAvailableState(
                        resourceLoader.GetString("AdvancedPaste_PhiSilicaNotReady_Title"),
                        resourceLoader.GetString("AdvancedPaste_PhiSilicaNotReady_Description"),
                        showPrepareButton: true,
                        details: diagnostics);
                }
                else
                {
                    _isPhiSilicaAvailable = true;
                    ShowPhiSilicaAvailableState(resourceLoader.GetString("AdvancedPaste_PhiSilicaAvailable_Message"));
                }
            }
            catch (Exception)
            {
                _isPhiSilicaAvailable = false;
                ShowPhiSilicaNotAvailableState(
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaNotAvailable_Title"),
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaCheckFailed_Description"));
            }

            if (ProviderConfigurationDialog is not null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = _isPhiSilicaAvailable;
            }
        }

        private void ShowPhiSilicaLoadingState(string message = null)
        {
            if (PhiSilicaLoadingText is not null && !string.IsNullOrEmpty(message))
            {
                PhiSilicaLoadingText.Text = message;
            }

            if (PhiSilicaLoadingPanel is not null)
            {
                PhiSilicaLoadingPanel.Visibility = Visibility.Visible;
            }

            if (PhiSilicaAvailablePanel is not null)
            {
                PhiSilicaAvailablePanel.Visibility = Visibility.Collapsed;
            }

            if (PhiSilicaNotAvailablePanel is not null)
            {
                PhiSilicaNotAvailablePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPhiSilicaAvailableState(string message)
        {
            if (PhiSilicaLoadingPanel is not null)
            {
                PhiSilicaLoadingPanel.Visibility = Visibility.Collapsed;
            }

            if (PhiSilicaAvailablePanel is not null)
            {
                PhiSilicaAvailablePanel.Visibility = Visibility.Visible;
            }

            if (PhiSilicaAvailableText is not null)
            {
                PhiSilicaAvailableText.Text = message;
            }

            if (PhiSilicaNotAvailablePanel is not null)
            {
                PhiSilicaNotAvailablePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPhiSilicaNotAvailableState(string title, string description, bool showPrepareButton = false, string details = null)
        {
            if (PhiSilicaLoadingPanel is not null)
            {
                PhiSilicaLoadingPanel.Visibility = Visibility.Collapsed;
            }

            if (PhiSilicaAvailablePanel is not null)
            {
                PhiSilicaAvailablePanel.Visibility = Visibility.Collapsed;
            }

            if (PhiSilicaNotAvailablePanel is not null)
            {
                PhiSilicaNotAvailablePanel.Visibility = Visibility.Visible;
            }

            if (PhiSilicaNotAvailableTitle is not null)
            {
                PhiSilicaNotAvailableTitle.Text = title;
            }

            if (PhiSilicaNotAvailableDescription is not null)
            {
                PhiSilicaNotAvailableDescription.Text = description;
            }

            if (PhiSilicaPrepareButton is not null)
            {
                PhiSilicaPrepareButton.Visibility = showPrepareButton ? Visibility.Visible : Visibility.Collapsed;
            }

            if (PhiSilicaNotAvailableDetails is not null)
            {
                PhiSilicaNotAvailableDetails.Text = details ?? string.Empty;
                PhiSilicaNotAvailableDetails.Visibility = string.IsNullOrWhiteSpace(details) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static (string Status, string Diagnostics) CheckPhiSilicaViaAdvancedPaste()
        {
            var settingsDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var advancedPastePath = Path.Combine(settingsDir, "PowerToys.AdvancedPaste.exe");

            if (!File.Exists(advancedPastePath))
            {
                return ("NotSupported", string.Empty);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = advancedPastePath,
                Arguments = "--check-phi-silica",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return ("NotSupported", string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10_000))
            {
                TryKillProcess(process);
                return ("NotSupported", string.Empty);
            }

            var output = outputTask.GetAwaiter().GetResult().Trim();
            var diagnostics = ExtractPhiSilicaDiagnostics(errorTask.GetAwaiter().GetResult());

            return output switch
            {
                "Available" => ("Available", diagnostics),
                "NotReady" => ("NotReady", diagnostics),
                _ => ("NotSupported", diagnostics),
            };
        }

        private static (string Status, string Diagnostics) PreparePhiSilicaViaAdvancedPaste()
        {
            var settingsDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var advancedPastePath = Path.Combine(settingsDir, "PowerToys.AdvancedPaste.exe");

            if (!File.Exists(advancedPastePath))
            {
                return ("NotSupported", string.Empty);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = advancedPastePath,
                Arguments = "--prepare-phi-silica",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return ("NotSupported", string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(600_000))
            {
                TryKillProcess(process);
                return ("Failed", string.Empty);
            }

            var output = outputTask.GetAwaiter().GetResult().Trim();
            var diagnostics = ExtractPhiSilicaDiagnostics(errorTask.GetAwaiter().GetResult());

            return output switch
            {
                "Ready" => ("Ready", diagnostics),
                "Failed" => ("Failed", diagnostics),
                _ => ("NotSupported", diagnostics),
            };
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
            }
        }

        private static string ExtractPhiSilicaDiagnostics(string standardError)
        {
            if (string.IsNullOrWhiteSpace(standardError))
            {
                return string.Empty;
            }

            const string prefix = "[phi-silica] ";
            var lines = standardError
                .Split(NewLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .Select(line => line.Substring(prefix.Length))
                .Distinct()
                .ToArray();

            return string.Join(Environment.NewLine, lines);
        }

        private async void PhiSilicaPrepareButton_Click(object sender, RoutedEventArgs e)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            ShowPhiSilicaLoadingState(resourceLoader.GetString("AdvancedPaste_PhiSilicaPreparing_Status"));

            if (ProviderConfigurationDialog is not null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
            }

            (string Status, string Diagnostics) prepareResult;
            try
            {
                prepareResult = await Task.Run(() => PreparePhiSilicaViaAdvancedPaste());
            }
            catch (Exception ex)
            {
                prepareResult = ("Failed", ex.Message);
            }

            if (prepareResult.Status == "Ready")
            {
                await UpdatePhiSilicaUIAsync();
                return;
            }

            _isPhiSilicaAvailable = false;

            if (prepareResult.Status == "NotSupported")
            {
                ShowPhiSilicaNotAvailableState(
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaNotAvailable_Title"),
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaNotAvailable_Description"),
                    details: prepareResult.Diagnostics);
            }
            else
            {
                ShowPhiSilicaNotAvailableState(
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaNotReady_Title"),
                    resourceLoader.GetString("AdvancedPaste_PhiSilicaPrepareFailed_Description"),
                    showPrepareButton: true,
                    details: prepareResult.Diagnostics);
            }

            if (ProviderConfigurationDialog is not null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
            }
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
            }
            catch (Exception ex)
            {
                var errorMessage = $"Unable to load Foundry Local models. {ex.Message}";
                DispatcherQueue.TryEnqueue(() => ShowFoundryUnavailableState(errorMessage));
            }
            finally
            {
                DispatcherQueue.TryEnqueue(UpdateFoundrySaveButtonState);
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
                    FoundryLocalPicker.StatusText = "Select a downloaded model from the list.";
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
        }

        private void RestoreFoundrySelection(IReadOnlyCollection<ModelDetails> cachedModels)
        {
            if (FoundryLocalPicker is null)
            {
                return;
            }

            var currentModelReference = ViewModel?.ProviderDraft?.ModelName;
            ModelDetails matchingModel = null;

            if (!string.IsNullOrWhiteSpace(currentModelReference))
            {
                matchingModel = cachedModels.FirstOrDefault(model =>
                    string.Equals(model.Name, currentModelReference, StringComparison.OrdinalIgnoreCase));
            }

            _suppressFoundrySelectionChanged = true;
            FoundryLocalPicker.SelectedModel = matchingModel;
            _suppressFoundrySelectionChanged = false;

            if (matchingModel is null)
            {
                if (ViewModel?.ProviderDraft is not null)
                {
                    ViewModel.ProviderDraft.ModelName = string.Empty;
                }

                FoundryLocalPicker.StatusText = _foundryCachedModels.Count == 0
                    ? "No local models detected. Use the button below to list models and download them with Foundry Local."
                    : "Select a downloaded model from the list.";
            }
            else if (ViewModel?.ProviderDraft is not null)
            {
                ViewModel.ProviderDraft.ModelName = matchingModel.Name;
                FoundryLocalPicker.StatusText = $"{matchingModel.Name} selected.";
            }

            UpdateFoundrySaveButtonState();
        }

        private void UpdateFoundrySaveButtonState()
        {
            if (ProviderConfigurationDialog is null)
            {
                return;
            }

            bool isFoundrySelected = string.Equals(ViewModel?.ProviderDraft?.ServiceType, "FoundryLocal", StringComparison.OrdinalIgnoreCase);

            if (!isFoundrySelected || ViewModel?.ProviderDraft is null)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = true;
                return;
            }

            bool isAllowedByGPO = ViewModel?.IsServiceTypeAllowedByGPO(AIServiceType.FoundryLocal) ?? true;
            if (!isAllowedByGPO || !_isFoundryLocalAvailable)
            {
                ProviderConfigurationDialog.IsPrimaryButtonEnabled = false;
                return;
            }

            ProviderConfigurationDialog.IsPrimaryButtonEnabled = FoundryLocalPicker?.SelectedModel is ModelDetails;
        }

        private void FoundryLocalPicker_SelectionChanged(object sender, ModelDetails selectedModel)
        {
            if (_suppressFoundrySelectionChanged)
            {
                return;
            }

            if (selectedModel is not null)
            {
                if (ViewModel?.ProviderDraft is not null)
                {
                    ViewModel.ProviderDraft.ModelName = selectedModel.Name;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = $"{selectedModel.Name} selected.";
                }
            }
            else
            {
                if (ViewModel?.ProviderDraft is not null)
                {
                    ViewModel.ProviderDraft.ModelName = string.Empty;
                }

                if (FoundryLocalPicker is not null)
                {
                    FoundryLocalPicker.StatusText = "Select a downloaded model from the list.";
                }
            }

            UpdateFoundrySaveButtonState();
        }

        private async void FoundryLocalPicker_LoadRequested(object sender)
        {
            await LoadFoundryLocalModelsAsync();
        }

        private void ProviderConfigurationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var draft = ViewModel?.ProviderDraft;
            if (draft is null)
            {
                args.Cancel = true;
                return;
            }

            NormalizeSystemPrompt(draft);
            string serviceType = draft.ServiceType ?? "OpenAI";
            string trimmedApiKey = ApiKeyPasswordBox.Password?.Trim() ?? string.Empty;
            var serviceKind = draft.ServiceTypeKind;
            bool requiresEndpoint = RequiresEndpointForService(serviceKind);
            string endpoint = (draft.EndpointUrl ?? string.Empty).Trim();

            if (!requiresEndpoint)
            {
                endpoint = string.Empty;
            }
            else if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = GetEndpointPlaceholder(serviceKind);
            }

            if (RequiresApiKeyForService(serviceType) && string.IsNullOrWhiteSpace(trimmedApiKey))
            {
                args.Cancel = true;
                return;
            }

            ViewModel.CommitProviderDraft(trimmedApiKey, endpoint);
            ApiKeyPasswordBox.Password = string.Empty;
        }

        private void EnableAdvancedAICheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            var draft = ViewModel?.ProviderDraft;
            if (draft is null)
            {
                return;
            }

            NormalizeSystemPrompt(draft);
            UpdateSystemPromptPlaceholder();
        }

        private static bool RequiresApiKeyForService(string serviceType)
        {
            return serviceType.ToAIServiceType() switch
            {
                AIServiceType.Onnx => false,
                AIServiceType.Ollama => false,
                AIServiceType.FoundryLocal => false,
                AIServiceType.PhiSilica => false,
                AIServiceType.ML => false,
                _ => true,
            };
        }

        private static bool RequiresEndpointForService(AIServiceType serviceKind)
        {
            return serviceKind is AIServiceType.AzureOpenAI
                or AIServiceType.AzureAIInference
                or AIServiceType.Mistral
                or AIServiceType.Ollama;
        }

        private static string GetEndpointPlaceholder(AIServiceType serviceKind)
        {
            return serviceKind switch
            {
                AIServiceType.AzureOpenAI => "https://your-resource.openai.azure.com/",
                AIServiceType.AzureAIInference => "https://{resource-name}.cognitiveservices.azure.com/",
                AIServiceType.Mistral => "https://api.mistral.ai/v1/",
                AIServiceType.Ollama => "http://localhost:11434/",
                _ => string.Empty,
            };
        }

        private string GetServiceLegalDescription(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return string.IsNullOrWhiteSpace(metadata.LegalDescription)
                ? string.Empty
                : ResourceLoaderInstance.ResourceLoader.GetString(metadata.LegalDescription);
        }

        private string GetServiceTermsLabel(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return string.IsNullOrWhiteSpace(metadata.TermsLabel)
                ? string.Empty
                : ResourceLoaderInstance.ResourceLoader.GetString(metadata.TermsLabel);
        }

        private Uri GetServiceTermsUri(string serviceType) => AIServiceTypeRegistry.GetMetadata(serviceType).TermsUri;

        private string GetServicePrivacyLabel(string serviceType)
        {
            var metadata = AIServiceTypeRegistry.GetMetadata(serviceType);
            return string.IsNullOrWhiteSpace(metadata.PrivacyLabel)
                ? string.Empty
                : ResourceLoaderInstance.ResourceLoader.GetString(metadata.PrivacyLabel);
        }

        private Uri GetServicePrivacyUri(string serviceType) => AIServiceTypeRegistry.GetMetadata(serviceType).PrivacyUri;

        private Visibility GetServiceLegalVisibility(string serviceType) =>
            AIServiceTypeRegistry.GetMetadata(serviceType).HasLegalInfo ? Visibility.Visible : Visibility.Collapsed;

        private Visibility GetServiceTermsVisibility(string serviceType) =>
            AIServiceTypeRegistry.GetMetadata(serviceType).HasTermsLink ? Visibility.Visible : Visibility.Collapsed;

        private Visibility GetServicePrivacyVisibility(string serviceType) =>
            AIServiceTypeRegistry.GetMetadata(serviceType).HasPrivacyLink ? Visibility.Visible : Visibility.Collapsed;

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

        private static void NormalizeSystemPrompt(AIProviderDefinition draft)
        {
            if (draft is not null && IsPlaceholderSystemPrompt(draft.SystemPrompt))
            {
                draft.SystemPrompt = string.Empty;
            }
        }

        private void UpdateSystemPromptPlaceholder()
        {
            var draft = ViewModel?.ProviderDraft;
            if (draft is null || SystemPromptTextBox is null)
            {
                return;
            }

            NormalizeSystemPrompt(draft);
            bool useAdvancedPlaceholder = EnableAdvancedAICheckBox?.IsOn ?? draft.EnableAdvancedAI;
            SystemPromptTextBox.PlaceholderText = useAdvancedPlaceholder ? AdvancedAISystemPrompt : SimpleAISystemPrompt;
        }

        private void RefreshDialogBindings()
        {
            try
            {
                Bindings?.Update();
            }
            catch (Exception)
            {
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

            menuFlyout.Items.Clear();

            menuFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = "Online models",
                FontSize = 12,
                IsEnabled = false,
                IsHitTestVisible = false,
            });

            foreach (var metadata in AIServiceTypeRegistry.GetOnlineServiceTypes())
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

            menuFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = "Local models",
                FontSize = 12,
                IsEnabled = false,
                IsHitTestVisible = false,
                Margin = new Thickness(0, 16, 0, 0),
            });

            foreach (var metadata in AIServiceTypeRegistry.GetLocalServiceTypes())
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

            if (ViewModel is null || ProviderConfigurationDialog is null)
            {
                return;
            }

            string serviceType = tag.Trim();
            string displayName = string.IsNullOrWhiteSpace(menuItem.Text) ? serviceType : menuItem.Text.Trim();

            ViewModel.BeginAddProvider(serviceType);
            if (ViewModel.ProviderDraft is null)
            {
                return;
            }

            ProviderConfigurationDialog.Title = string.IsNullOrWhiteSpace(displayName)
                ? DialogDefaultTitle
                : $"{displayName} provider configuration";

            await UpdateFoundryLocalUIAsync();
            await UpdatePhiSilicaUIAsync();
            UpdateProviderUIVisibility();
            RefreshDialogBindings();
            ApiKeyPasswordBox.Password = string.Empty;
            await ProviderConfigurationDialog.ShowAsync();
        }

        private async void EditProviderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem menuItem || menuItem.Tag is not AIProviderDefinition provider)
            {
                return;
            }

            if (ViewModel is null || ProviderConfigurationDialog is null)
            {
                return;
            }

            ViewModel.BeginEditProvider(provider);

            string titlePrefix = string.IsNullOrWhiteSpace(provider.ModelName) ? provider.ServiceType : provider.ModelName;
            ProviderConfigurationDialog.Title = string.IsNullOrWhiteSpace(titlePrefix)
                ? DialogDefaultTitle
                : $"{titlePrefix} provider configuration";

            UpdateProviderUIVisibility();
            await UpdateFoundryLocalUIAsync();
            await UpdatePhiSilicaUIAsync();
            RefreshDialogBindings();
            ApiKeyPasswordBox.Password = ViewModel.GetApiKey(provider.Id, provider.ServiceType);
            await ProviderConfigurationDialog.ShowAsync();
        }

        private void RemoveProviderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is AIProviderDefinition provider)
            {
                ViewModel?.RemoveProvider(provider);
            }
        }

        private void SetAsDefaultProviderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is AIProviderDefinition provider)
            {
                ViewModel?.SetAsDefaultProvider(provider);
            }
        }

        private void ProviderConfigurationDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel?.CancelProviderDraft();
            ProviderConfigurationDialog.Title = DialogDefaultTitle;
            ApiKeyPasswordBox.Password = string.Empty;
        }
    }
}
