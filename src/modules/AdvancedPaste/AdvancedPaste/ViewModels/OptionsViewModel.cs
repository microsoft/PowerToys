// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinUIEx;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace AdvancedPaste.ViewModels
{
    public partial class OptionsViewModel : ObservableObject, IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly DispatcherTimer _clipboardTimer;
        private readonly IUserSettings _userSettings;
        private readonly AICompletionsHelper aiHelper;
        private readonly App app = App.Current as App;

        public DataPackageView ClipboardData { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClipboardHasData))]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxPlaceholderText))]
        [NotifyPropertyChangedFor(nameof(GeneralErrorText))]
        private ClipboardFormat _availableClipboardFormats;

        [ObservableProperty]
        private bool _clipboardHistoryEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxPlaceholderText))]
        [NotifyPropertyChangedFor(nameof(GeneralErrorText))]
        [NotifyPropertyChangedFor(nameof(IsCustomAIEnabled))]
        private bool _isAllowedByGPO;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ApiErrorText))]
        private int _apiRequestStatus;

        [ObservableProperty]
        private string _query = string.Empty;

        private bool _pasteFormatsDirty;

        public ObservableCollection<PasteFormat> StandardPasteFormats { get; } = [];

        public ObservableCollection<PasteFormat> CustomActionPasteFormats { get; } = [];

        public bool IsCustomAIEnabled => IsAllowedByGPO && aiHelper.IsAIEnabled;

        public bool ClipboardHasData => AvailableClipboardFormats != ClipboardFormat.None;

        public event EventHandler<CustomActionActivatedEventArgs> CustomActionActivated;

        public OptionsViewModel(IUserSettings userSettings)
        {
            aiHelper = new AICompletionsHelper();
            _userSettings = userSettings;

            ApiRequestStatus = (int)HttpStatusCode.OK;

            GeneratedResponses = new ObservableCollection<string>();
            GeneratedResponses.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasMultipleResponses));
                OnPropertyChanged(nameof(CurrentIndexDisplay));
            };

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            ReadClipboard();
            _clipboardTimer = new() { Interval = TimeSpan.FromSeconds(1) };
            _clipboardTimer.Tick += ClipboardTimer_Tick;
            _clipboardTimer.Start();

            RefreshPasteFormats();
            _userSettings.Changed += (_, _) => EnqueueRefreshPasteFormats();
            PropertyChanged += (_, e) =>
            {
                string[] dirtyingProperties = [nameof(Query), nameof(IsCustomAIEnabled), nameof(AvailableClipboardFormats)];

                if (dirtyingProperties.Contains(e.PropertyName))
                {
                    EnqueueRefreshPasteFormats();
                }
            };
        }

        private void ClipboardTimer_Tick(object sender, object e)
        {
            if (app.GetMainWindow()?.Visible is true)
            {
                ReadClipboard();
                UpdateAllowedByGPO();
            }
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

        private PasteFormat CreatePasteFormat(PasteFormats format) => new(format, AvailableClipboardFormats, IsCustomAIEnabled, ResourceLoaderInstance.ResourceLoader.GetString);

        private PasteFormat CreatePasteFormat(AdvancedPasteCustomAction customAction) => new(customAction, AvailableClipboardFormats, IsCustomAIEnabled);

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
                collection.Clear();

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
                                                    .Select(CreatePasteFormat));

            UpdateFormats(CustomActionPasteFormats, _userSettings.CustomActions.Select(CreatePasteFormat));
        }

        public void Dispose()
        {
            _clipboardTimer.Stop();
            GC.SuppressFinalize(this);
        }

        public void ReadClipboard()
        {
            ClipboardData = Clipboard.GetContent();

            (string DataFormat, ClipboardFormat ClipboardFormat)[] formats =
            [
                (StandardDataFormats.Text, ClipboardFormat.Text),
                (StandardDataFormats.Html, ClipboardFormat.Html),
                (StandardDataFormats.Bitmap, ClipboardFormat.Image),
                (StandardDataFormats.StorageItems, ClipboardFormat.File),
            ];

            AvailableClipboardFormats = formats.Aggregate(
                ClipboardFormat.None,
                (result, formatTuple) => ClipboardData.Contains(formatTuple.DataFormat) ? (result | formatTuple.ClipboardFormat) : result);
        }

        public void OnShow()
        {
            ReadClipboard();
            UpdateAllowedByGPO();

            if (IsAllowedByGPO)
            {
                var openAIKey = AICompletionsHelper.LoadOpenAIKey();
                var currentKey = aiHelper.GetKey();
                bool keyChanged = openAIKey != currentKey;

                if (keyChanged)
                {
                    app.GetMainWindow().StartLoading();

                    Task.Run(() =>
                    {
                        aiHelper.SetOpenAIKey(openAIKey);
                    }).ContinueWith(
                        (t) =>
                        {
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                app.GetMainWindow().FinishLoading(aiHelper.IsAIEnabled);
                                OnPropertyChanged(nameof(InputTxtBoxPlaceholderText));
                                OnPropertyChanged(nameof(GeneralErrorText));
                                OnPropertyChanged(nameof(IsCustomAIEnabled));
                            });
                        },
                        TaskScheduler.Default);
                }
            }

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            GeneratedResponses.Clear();
        }

        // List to store generated responses
        public ObservableCollection<string> GeneratedResponses { get; set; } = new ObservableCollection<string>();

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

        public bool HasMultipleResponses
        {
            get => GeneratedResponses.Count > 1;
        }

        public string CurrentIndexDisplay => $"{CurrentResponseIndex + 1}/{GeneratedResponses.Count}";

        public string InputTxtBoxPlaceholderText
        {
            get
            {
                app.GetMainWindow().ClearInputText();

                return ClipboardHasData ? ResourceLoaderInstance.ResourceLoader.GetString("CustomFormatTextBox/PlaceholderText") : GeneralErrorText;
            }
        }

        public string GeneralErrorText
        {
            get
            {
                if (!ClipboardHasData)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("ClipboardEmptyWarning");
                }

                if (!IsAllowedByGPO)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("OpenAIGpoDisabled");
                }

                if (!aiHelper.IsAIEnabled)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("OpenAINotConfigured");
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public string ApiErrorText
        {
            get => (HttpStatusCode)ApiRequestStatus switch
            {
                HttpStatusCode.TooManyRequests => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyTooManyRequests"),
                HttpStatusCode.Unauthorized => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyUnauthorized"),
                HttpStatusCode.OK => string.Empty,
                _ => ResourceLoaderInstance.ResourceLoader.GetString("OpenAIApiKeyError") + ApiRequestStatus.ToString(CultureInfo.InvariantCulture),
            };
        }

        [ObservableProperty]
        private string _customFormatResult;

        [RelayCommand]
        public void PasteCustom()
        {
            var text = GeneratedResponses.ElementAtOrDefault(CurrentResponseIndex);

            if (text != null)
            {
                PasteCustomFunction(text);
            }
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
            (App.Current as App).GetMainWindow().Close();
        }

        private void SetClipboardContentAndHideWindow(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                ClipboardHelper.SetClipboardTextContent(content);
            }

            if (app.GetMainWindow() != null)
            {
                Windows.Win32.Foundation.HWND hwnd = (Windows.Win32.Foundation.HWND)app.GetMainWindow().GetWindowHandle();
                Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
            }
        }

        internal void ToPlainTextFunction()
        {
            try
            {
                Logger.LogTrace();

                string outputString = MarkdownHelper.PasteAsPlainTextFromClipboard(ClipboardData);

                SetClipboardContentAndHideWindow(outputString);

                if (_userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }
        }

        internal void ToMarkdownFunction(bool pasteAlways = false)
        {
            try
            {
                Logger.LogTrace();

                string outputString = MarkdownHelper.ToMarkdown(ClipboardData);

                SetClipboardContentAndHideWindow(outputString);

                if (pasteAlways || _userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }
        }

        internal void ToJsonFunction(bool pasteAlways = false)
        {
            try
            {
                Logger.LogTrace();

                string jsonText = JsonHelper.ToJsonFromXmlOrCsv(ClipboardData);

                SetClipboardContentAndHideWindow(jsonText);

                if (pasteAlways || _userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }
        }

        internal void ImageToTextFunction()
        {
            Task.Factory
                .StartNew(async () => await ImageToTextFunctionAsync(), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        internal async Task ImageToTextFunctionAsync(bool pasteAlways = false)
        {
            try
            {
                Logger.LogTrace();

                var bitmap = await ClipboardHelper.GetClipboardImageContentAsync(ClipboardData);
                var text = await OcrHelpers.GetTextAsync(bitmap);
                SetClipboardContentAndHideWindow(text);

                if (pasteAlways || _userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to extract text from image", ex);

                await app.GetMainWindow().ShowMessageDialogAsync(ResourceLoaderInstance.ResourceLoader.GetString("PasteError"));
            }
        }

        internal void ExecutePasteFormat(VirtualKey key)
        {
            var pasteFormat = StandardPasteFormats.Concat(CustomActionPasteFormats)
                                                  .Where(pasteFormat => pasteFormat.IsEnabled)
                                                  .ElementAtOrDefault(key - VirtualKey.Number1);

            if (pasteFormat != null)
            {
                ExecutePasteFormat(pasteFormat);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(pasteFormat.Format));
            }
        }

        internal void ExecutePasteFormat(PasteFormat pasteFormat)
        {
            if (!pasteFormat.IsEnabled)
            {
                return;
            }

            switch (pasteFormat.Format)
            {
                case PasteFormats.PlainText:
                    ToPlainTextFunction();
                    break;

                case PasteFormats.Markdown:
                    ToMarkdownFunction();
                    break;

                case PasteFormats.Json:
                    ToJsonFunction();
                    break;

                case PasteFormats.AudioToText:
                    throw new NotImplementedException();

                case PasteFormats.ImageToText:
                    ImageToTextFunction();
                    break;

                case PasteFormats.PasteAsTxtFile:
                    throw new NotImplementedException();

                case PasteFormats.PasteAsPngFile:
                    throw new NotImplementedException();

                case PasteFormats.PasteAsHtmlFile:
                    throw new NotImplementedException();

                case PasteFormats.Custom:
                    Query = pasteFormat.Prompt;
                    CustomActionActivated?.Invoke(this, new CustomActionActivatedEventArgs(pasteFormat.Prompt, false));
                    break;
            }
        }

        internal void ExecuteAdditionalAction(PasteFormats format)
        {
            ExecutePasteFormat(CreatePasteFormat(format));
        }

        internal void ExecuteCustomActionWithPaste(int customActionId)
        {
            Logger.LogTrace();

            var customAction = _userSettings.CustomActions.FirstOrDefault(customAction => customAction.Id == customActionId);

            if (customAction != null)
            {
                Query = customAction.Prompt;
                CustomActionActivated?.Invoke(this, new CustomActionActivatedEventArgs(customAction.Prompt, true));
            }
        }

        internal async Task<string> GenerateCustomFunction(string inputInstructions)
        {
            Logger.LogTrace();

            if (string.IsNullOrWhiteSpace(inputInstructions))
            {
                return string.Empty;
            }

            if (!AvailableClipboardFormats.HasFlag(ClipboardFormat.Text))
            {
                Logger.LogWarning("Clipboard does not contain text data");
                return string.Empty;
            }

            string currentClipboardText = await Task.Run(async () =>
            {
                try
                {
                    string text = await ClipboardData.GetTextAsync();
                    return text;
                }
                catch (Exception)
                {
                    // Couldn't get text from the clipboard. Resume with empty text.
                    return string.Empty;
                }
            });

            if (string.IsNullOrWhiteSpace(currentClipboardText))
            {
                Logger.LogWarning("Clipboard has no usable text data");
                return string.Empty;
            }

            var aiResponse = await Task.Run(() => aiHelper.AIFormatString(inputInstructions, currentClipboardText));

            string aiOutput = aiResponse.Response;
            ApiRequestStatus = aiResponse.ApiRequestStatus;

            GeneratedResponses.Add(aiOutput);
            CurrentResponseIndex = GeneratedResponses.Count - 1;
            return aiOutput;
        }

        internal void PasteCustomFunction(string text)
        {
            Logger.LogTrace();

            SetClipboardContentAndHideWindow(text);

            if (_userSettings.SendPasteKeyCombination)
            {
                ClipboardHelper.SendPasteKeyCombination();
            }
        }

        internal CustomQuery RecallPreviousCustomQuery()
        {
            return LoadPreviousQuery();
        }

        internal void SaveQuery(string inputQuery)
        {
            Logger.LogTrace();

            DataPackageView clipboardData = Clipboard.GetContent();

            if (clipboardData == null || !clipboardData.Contains(StandardDataFormats.Text))
            {
                Logger.LogWarning("Clipboard does not contain text data");
                return;
            }

            var currentClipboardText = Task.Run(async () => await clipboardData.GetTextAsync()).Result;

            var queryData = new CustomQuery
            {
                Query = inputQuery,
                ClipboardData = currentClipboardText,
            };

            SettingsUtils utils = new SettingsUtils();
            utils.SaveSettings(queryData.ToString(), Constants.AdvancedPasteModuleName, Constants.LastQueryJsonFileName);
        }

        internal CustomQuery LoadPreviousQuery()
        {
            SettingsUtils utils = new SettingsUtils();
            var query = utils.GetSettings<CustomQuery>(Constants.AdvancedPasteModuleName, Constants.LastQueryJsonFileName);
            return query;
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
    }
}
