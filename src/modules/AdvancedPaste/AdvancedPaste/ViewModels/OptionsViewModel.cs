// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinUIEx;

using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace AdvancedPaste.ViewModels
{
    public sealed partial class OptionsViewModel : ObservableObject, IProgress<double>, IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly DispatcherTimer _clipboardTimer;
        private readonly IUserSettings _userSettings;
        private readonly IPasteFormatExecutor _pasteFormatExecutor;
        private readonly IAICredentialsProvider _aiCredentialsProvider;

        private CancellationTokenSource _pasteActionCancellationTokenSource;

        public DataPackageView ClipboardData { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomAIAvailable))]
        [NotifyPropertyChangedFor(nameof(ClipboardHasData))]
        [NotifyPropertyChangedFor(nameof(ClipboardHasDataForCustomAI))]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxPlaceholderText))]
        [NotifyPropertyChangedFor(nameof(CustomAIUnavailableErrorText))]
        private ClipboardFormat _availableClipboardFormats;

        [ObservableProperty]
        private bool _clipboardHistoryEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CustomAIUnavailableErrorText))]
        [NotifyPropertyChangedFor(nameof(IsCustomAIServiceEnabled))]
        [NotifyPropertyChangedFor(nameof(IsCustomAIAvailable))]
        private bool _isAllowedByGPO;

        [ObservableProperty]
        private PasteActionError _pasteActionError = PasteActionError.None;

        [ObservableProperty]
        private string _query = string.Empty;

        private bool _pasteFormatsDirty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasIndeterminateTransformProgress))]
        private double _transformProgress = double.NaN;

        public ObservableCollection<PasteFormat> StandardPasteFormats { get; } = [];

        public ObservableCollection<PasteFormat> CustomActionPasteFormats { get; } = [];

        public bool IsCustomAIServiceEnabled => IsAllowedByGPO && _aiCredentialsProvider.IsConfigured;

        public bool IsCustomAIAvailable => IsCustomAIServiceEnabled && ClipboardHasDataForCustomAI;

        public bool IsAdvancedAIEnabled => IsCustomAIServiceEnabled && _userSettings.IsAdvancedAIEnabled;

        public bool ClipboardHasData => AvailableClipboardFormats != ClipboardFormat.None;

        public bool ClipboardHasDataForCustomAI => PasteFormat.SupportsClipboardFormats(CustomAIFormat, AvailableClipboardFormats);

        public bool HasIndeterminateTransformProgress => double.IsNaN(TransformProgress);

        private PasteFormats CustomAIFormat => _userSettings.IsAdvancedAIEnabled ? PasteFormats.KernelQuery : PasteFormats.CustomTextTransformation;

        private bool Visible
        {
            get
            {
                try
                {
                    return GetMainWindow()?.Visible is true;
                }
                catch (COMException)
                {
                    return false; // window is closed
                }
            }
        }

        public event EventHandler PreviewRequested;

        public OptionsViewModel(IFileSystem fileSystem, IAICredentialsProvider aiCredentialsProvider, IUserSettings userSettings, IPasteFormatExecutor pasteFormatExecutor)
        {
            _aiCredentialsProvider = aiCredentialsProvider;
            _userSettings = userSettings;
            _pasteFormatExecutor = pasteFormatExecutor;

            GeneratedResponses = [];
            GeneratedResponses.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasMultipleResponses));
                OnPropertyChanged(nameof(CurrentIndexDisplay));
            };

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            UpdateOpenAIKey();
            _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(1) };
            _clipboardTimer.Tick += ClipboardTimer_Tick;
            _clipboardTimer.Start();

            RefreshPasteFormats();
            _userSettings.Changed += UserSettings_Changed;
            PropertyChanged += (_, e) =>
            {
                string[] dirtyingProperties = [nameof(Query), nameof(IsCustomAIServiceEnabled), nameof(IsCustomAIAvailable), nameof(AvailableClipboardFormats)];

                if (dirtyingProperties.Contains(e.PropertyName))
                {
                    EnqueueRefreshPasteFormats();
                }
            };

            try
            {
                // Delete file that is no longer needed but might have been written by previous version and contain sensitive information.
                fileSystem.File.Delete(new SettingsUtils(fileSystem).GetSettingsFilePath(Constants.AdvancedPasteModuleName, "lastQuery.json"));
            }
            catch
            {
            }
        }

        private static MainWindow GetMainWindow() => (App.Current as App)?.GetMainWindow();

        private async void ClipboardTimer_Tick(object sender, object e)
        {
            if (Visible)
            {
                await ReadClipboardAsync();
                UpdateAllowedByGPO();
            }
        }

        private void UserSettings_Changed(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(ClipboardHasDataForCustomAI));
            OnPropertyChanged(nameof(IsCustomAIAvailable));
            OnPropertyChanged(nameof(IsAdvancedAIEnabled));

            EnqueueRefreshPasteFormats();
        }

        private void EnqueueRefreshPasteFormats()
        {
            if (_pasteFormatsDirty)
            {
                return;
            }

            _pasteFormatsDirty = true;
            _dispatcherQueue.TryEnqueue(() =>
            {
                RefreshPasteFormats();
                _pasteFormatsDirty = false;
            });
        }

        private PasteFormat CreateStandardPasteFormat(PasteFormats format) =>
            PasteFormat.CreateStandardFormat(format, AvailableClipboardFormats, IsCustomAIServiceEnabled, ResourceLoaderInstance.ResourceLoader.GetString);

        private PasteFormat CreateCustomAIPasteFormat(string name, string prompt, bool isSavedQuery) =>
            PasteFormat.CreateCustomAIFormat(CustomAIFormat, name, prompt, isSavedQuery, AvailableClipboardFormats, IsCustomAIServiceEnabled);

        private void RefreshPasteFormats()
        {
            var ctrlString = ResourceLoaderInstance.ResourceLoader.GetString("CtrlKey");
            int shortcutNum = 0;

            string GetNextShortcutText()
            {
                shortcutNum++;
                return shortcutNum <= 9 ? $"{ctrlString}+{shortcutNum}" : string.Empty;
            }

            IEnumerable<PasteFormat> FilterAndSort(IEnumerable<PasteFormat> pasteFormats) =>
                from pasteFormat in pasteFormats
                let comparison = StringComparison.CurrentCultureIgnoreCase
                where pasteFormat.Name.Contains(Query, comparison) || pasteFormat.Prompt.Contains(Query, comparison)
                orderby pasteFormat.IsEnabled descending
                select pasteFormat;

            void UpdateFormats(ObservableCollection<PasteFormat> collection, IEnumerable<PasteFormat> pasteFormats)
            {
                // Hack: Clear collection via repeated RemoveAt to avoid this crash, which seems to occasionally occur when using Clear:
                // https://github.com/microsoft/microsoft-ui-xaml/issues/8684
                while (collection.Count > 0)
                {
                    collection.RemoveAt(collection.Count - 1);
                }

                foreach (var format in FilterAndSort(pasteFormats))
                {
                    if (format.IsEnabled)
                    {
                        format.ShortcutText = GetNextShortcutText();
                    }

                    collection.Add(format);
                }
            }

            UpdateFormats(StandardPasteFormats, Enum.GetValues<PasteFormats>()
                                                    .Where(format => PasteFormat.MetadataDict[format].IsCoreAction || _userSettings.AdditionalActions.Contains(format))
                                                    .Select(CreateStandardPasteFormat));

            UpdateFormats(
                CustomActionPasteFormats,
                IsCustomAIServiceEnabled ? _userSettings.CustomActions.Select(customAction => CreateCustomAIPasteFormat(customAction.Name, customAction.Prompt, isSavedQuery: true)) : []);
        }

        public void Dispose()
        {
            _clipboardTimer.Stop();
            _pasteActionCancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task ReadClipboardAsync()
        {
            if (IsBusy)
            {
                return;
            }

            ClipboardData = Clipboard.GetContent();
            AvailableClipboardFormats = await ClipboardData.GetAvailableFormatsAsync();
        }

        public async Task OnShowAsync()
        {
            PasteActionError = PasteActionError.None;
            Query = string.Empty;

            await ReadClipboardAsync();

            if (UpdateOpenAIKey())
            {
                GetMainWindow()?.StartLoading();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    GetMainWindow()?.FinishLoading(_aiCredentialsProvider.IsConfigured);
                    OnPropertyChanged(nameof(InputTxtBoxPlaceholderText));
                    OnPropertyChanged(nameof(CustomAIUnavailableErrorText));
                    OnPropertyChanged(nameof(IsCustomAIServiceEnabled));
                    OnPropertyChanged(nameof(IsAdvancedAIEnabled));
                    OnPropertyChanged(nameof(IsCustomAIAvailable));
                });
            }

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            GeneratedResponses.Clear();
        }

        // List to store generated responses
        public ObservableCollection<string> GeneratedResponses { get; set; } = [];

        // Index to keep track of the current response
        private int _currentResponseIndex;

        public int CurrentResponseIndex
        {
            get => _currentResponseIndex;
            set
            {
                if (value >= 0 && value < GeneratedResponses.Count)
                {
                    SetProperty(ref _currentResponseIndex, value);
                    CustomFormatResult = GeneratedResponses[_currentResponseIndex];
                    OnPropertyChanged(nameof(CurrentIndexDisplay));
                }
            }
        }

        public bool HasMultipleResponses => GeneratedResponses.Count > 1;

        public string CurrentIndexDisplay => $"{CurrentResponseIndex + 1}/{GeneratedResponses.Count}";

        public string InputTxtBoxPlaceholderText
            => ResourceLoaderInstance.ResourceLoader.GetString(ClipboardHasData ? "CustomFormatTextBox/PlaceholderText" : "ClipboardEmptyWarning");

        public string CustomAIUnavailableErrorText
        {
            get
            {
                if (!IsAllowedByGPO)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("OpenAIGpoDisabled");
                }

                if (!_aiCredentialsProvider.IsConfigured)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("OpenAINotConfigured");
                }

                if (!ClipboardHasDataForCustomAI)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("ClipboardEmptyWarning");
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        [ObservableProperty]
        private string _customFormatResult;

        [RelayCommand]
        public async Task PasteCustomAsync()
        {
            var text = GeneratedResponses.ElementAtOrDefault(CurrentResponseIndex);

            if (!string.IsNullOrEmpty(text))
            {
                await CopyPasteAndHideAsync(DataPackageHelpers.CreateFromText(text));
            }
        }

        private async Task CopyPasteAndHideAsync(DataPackage package)
        {
            await ClipboardHelper.TryCopyPasteAsync(package, HideWindow);
            Query = string.Empty;

            // Delete any temp files created. A delay is needed to ensure the file is not in use by the target application -
            // for example, when pasting onto File Explorer, the paste operation will trigger a file copy.
            _ = Task.Run(() => package.GetView().TryCleanupAfterDelayAsync(TimeSpan.FromSeconds(30)));
        }

        // Command to select the previous custom format
        [RelayCommand]
        public void PreviousCustomFormat()
        {
            if (CurrentResponseIndex > 0)
            {
                CurrentResponseIndex--;
            }
        }

        // Command to select the next custom format
        [RelayCommand]
        public void NextCustomFormat()
        {
            if (CurrentResponseIndex < GeneratedResponses.Count - 1)
            {
                CurrentResponseIndex++;
            }
        }

        // Command to open the Settings window.
        [RelayCommand]
        public void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.AdvancedPaste, true);
            GetMainWindow()?.Close();
        }

        internal async Task ExecutePasteFormatAsync(PasteFormats format, PasteActionSource source)
        {
            await ReadClipboardAsync();
            await ExecutePasteFormatAsync(CreateStandardPasteFormat(format), source);
        }

        internal async Task ExecutePasteFormatAsync(PasteFormat pasteFormat, PasteActionSource source)
        {
            if (IsBusy)
            {
                Logger.LogWarning($"Execution of {pasteFormat.Format} from {source} suppressed as busy");
                return;
            }

            if (!pasteFormat.IsEnabled)
            {
                PasteActionError = PasteActionError.FromResourceId(pasteFormat.SupportsClipboardFormats(AvailableClipboardFormats) ? "PasteError" : "ClipboardEmptyWarning");
                return;
            }

            var elapsedWatch = Stopwatch.StartNew();
            Logger.LogDebug($"Started executing {pasteFormat.Format} from source {source}");

            IsBusy = true;
            _pasteActionCancellationTokenSource = new();
            TransformProgress = double.NaN;
            PasteActionError = PasteActionError.None;
            Query = pasteFormat.Query;

            try
            {
                // Minimum time to show busy spinner for AI actions when triggered by global keyboard shortcut.
                var aiActionMinTaskTime = TimeSpan.FromSeconds(1.5);
                var delayTask = (Visible && source == PasteActionSource.GlobalKeyboardShortcut) ? Task.Delay(aiActionMinTaskTime) : Task.CompletedTask;
                var dataPackage = await _pasteFormatExecutor.ExecutePasteFormatAsync(pasteFormat, source, _pasteActionCancellationTokenSource.Token, this);

                await delayTask;

                var outputText = await dataPackage.GetView().GetTextOrEmptyAsync();
                bool shouldPreview = pasteFormat.Metadata.CanPreview && _userSettings.ShowCustomPreview && !string.IsNullOrEmpty(outputText) && source != PasteActionSource.GlobalKeyboardShortcut;

                if (shouldPreview)
                {
                    GeneratedResponses.Add(outputText);
                    CurrentResponseIndex = GeneratedResponses.Count - 1;
                    PreviewRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    await CopyPasteAndHideAsync(dataPackage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error executing paste format", ex);
                PasteActionError = PasteActionError.FromException(ex);
            }

            IsBusy = false;
            _pasteActionCancellationTokenSource?.Dispose();
            _pasteActionCancellationTokenSource = null;
            elapsedWatch.Stop();
            Logger.LogDebug($"Finished executing {pasteFormat.Format} from source {source}; timeTakenMs={elapsedWatch.ElapsedMilliseconds}");
        }

        internal async Task ExecutePasteFormatAsync(VirtualKey key)
        {
            var pasteFormat = StandardPasteFormats.Concat(CustomActionPasteFormats)
                                                  .Where(pasteFormat => pasteFormat.IsEnabled)
                                                  .ElementAtOrDefault(key - VirtualKey.Number1);

            if (pasteFormat != null)
            {
                await ExecutePasteFormatAsync(pasteFormat, PasteActionSource.InAppKeyboardShortcut);
            }
        }

        internal async Task ExecuteCustomActionAsync(int customActionId, PasteActionSource source)
        {
            Logger.LogTrace();

            var customAction = _userSettings.CustomActions.FirstOrDefault(customAction => customAction.Id == customActionId);

            if (customAction != null)
            {
                await ReadClipboardAsync();
                await ExecutePasteFormatAsync(CreateCustomAIPasteFormat(customAction.Name, customAction.Prompt, isSavedQuery: true), source);
            }
        }

        internal async Task ExecuteCustomAIFormatFromCurrentQueryAsync(PasteActionSource triggerSource)
        {
            var customAction = _userSettings.CustomActions
                                            .FirstOrDefault(customAction => Models.KernelQueryCache.CacheKey.PromptComparer.Equals(customAction.Prompt, Query));

            await ExecutePasteFormatAsync(CreateCustomAIPasteFormat(customAction?.Name ?? "Default", Query, isSavedQuery: customAction != null), triggerSource);
        }

        private void HideWindow()
        {
            var mainWindow = GetMainWindow();

            if (mainWindow != null)
            {
                Windows.Win32.Foundation.HWND hwnd = (Windows.Win32.Foundation.HWND)mainWindow.GetWindowHandle();
                Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
            }
        }

        private bool IsClipboardHistoryEnabled()
        {
            string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Clipboard\";
            try
            {
                int enableClipboardHistory = (int)Registry.GetValue(registryKey, "EnableClipboardHistory", false);
                return enableClipboardHistory != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void UpdateAllowedByGPO()
        {
            IsAllowedByGPO = PowerToys.GPOWrapper.GPOWrapper.GetAllowedAdvancedPasteOnlineAIModelsValue() != PowerToys.GPOWrapper.GpoRuleConfigured.Disabled;
        }

        private bool UpdateOpenAIKey()
        {
            UpdateAllowedByGPO();

            return IsAllowedByGPO && _aiCredentialsProvider.Refresh();
        }

        public async Task CancelPasteActionAsync()
        {
            if (_pasteActionCancellationTokenSource != null)
            {
                await _pasteActionCancellationTokenSource.CancelAsync();
            }
        }

        void IProgress<double>.Report(double value)
        {
            ReportProgress(value);
        }

        private void ReportProgress(double value)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                TransformProgress = value;
            });
        }
    }
}
