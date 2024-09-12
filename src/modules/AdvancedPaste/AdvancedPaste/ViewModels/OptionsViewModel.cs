// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
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
        private readonly PasteFormat[] _allStandardPasteFormats;

        public DataPackageView ClipboardData { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxPlaceholderText))]
        [NotifyPropertyChangedFor(nameof(GeneralErrorText))]
        [NotifyPropertyChangedFor(nameof(IsCustomAIEnabled))]
        private bool _isClipboardDataText;

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

        public bool IsCustomAIEnabled => IsAllowedByGPO && IsClipboardDataText && aiHelper.IsAIEnabled;

        public event EventHandler<CustomActionActivatedEventArgs> CustomActionActivated;

        public OptionsViewModel(IUserSettings userSettings)
        {
            aiHelper = new AICompletionsHelper();
            _userSettings = userSettings;

            ApiRequestStatus = (int)HttpStatusCode.OK;

            _allStandardPasteFormats =
            [
                new PasteFormat { IconGlyph = "\uE8E9", Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsPlainText"), Format = PasteFormats.PlainText },
                new PasteFormat { IconGlyph = "\ue8a5", Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsMarkdown"), Format = PasteFormats.Markdown },
                new PasteFormat { IconGlyph = "\uE943", Name = ResourceLoaderInstance.ResourceLoader.GetString("PasteAsJson"), Format = PasteFormats.Json },
            ];

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
            _userSettings.CustomActions.CollectionChanged += (_, _) => EnqueueRefreshPasteFormats();
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Query))
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

        private void RefreshPasteFormats()
        {
            bool Filter(string text) => text.Contains(Query, StringComparison.CurrentCultureIgnoreCase);

            var ctrlString = ResourceLoaderInstance.ResourceLoader.GetString("CtrlKey");
            int shortcutNum = 0;

            string GetNextShortcutText()
            {
                shortcutNum++;
                return shortcutNum <= 9 ? $"{ctrlString}+{shortcutNum}" : string.Empty;
            }

            StandardPasteFormats.Clear();
            foreach (var format in _allStandardPasteFormats)
            {
                if (Filter(format.Name))
                {
                    format.ShortcutText = GetNextShortcutText();
                    format.ToolTip = $"{format.Name} ({format.ShortcutText})";
                    StandardPasteFormats.Add(format);
                }
            }

            CustomActionPasteFormats.Clear();
            foreach (var customAction in _userSettings.CustomActions)
            {
                if (Filter(customAction.Name) || Filter(customAction.Prompt))
                {
                    CustomActionPasteFormats.Add(new PasteFormat(customAction, GetNextShortcutText()));
                }
            }
        }

        public void Dispose()
        {
            _clipboardTimer.Stop();
            GC.SuppressFinalize(this);
        }

        public void ReadClipboard()
        {
            ClipboardData = Clipboard.GetContent();
            IsClipboardDataText = ClipboardData.Contains(StandardDataFormats.Text);
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

                return IsClipboardDataText ? ResourceLoaderInstance.ResourceLoader.GetString("CustomFormatTextBox/PlaceholderText") : GeneralErrorText;
            }
        }

        public string GeneralErrorText
        {
            get
            {
                if (!IsClipboardDataText)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("ClipboardDataTypeMismatchWarning");
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

        internal void ExecutePasteFormat(VirtualKey key)
        {
            var index = key - VirtualKey.Number1;
            var pasteFormat = StandardPasteFormats.ElementAtOrDefault(index) ?? CustomActionPasteFormats.ElementAtOrDefault(index - StandardPasteFormats.Count);

            if (pasteFormat != null)
            {
                ExecutePasteFormat(pasteFormat);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteInAppKeyboardShortcutEvent(pasteFormat.Format));
            }
        }

        internal void ExecutePasteFormat(PasteFormat pasteFormat)
        {
            if (!IsClipboardDataText || (pasteFormat.Format == PasteFormats.Custom && !IsCustomAIEnabled))
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

                case PasteFormats.Custom:
                    Query = pasteFormat.Prompt;
                    CustomActionActivated?.Invoke(this, new CustomActionActivatedEventArgs(pasteFormat.Prompt, false));
                    break;
            }
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

            if (!IsClipboardDataText)
            {
                Logger.LogWarning("Clipboard does not contain text data");
                return string.Empty;
            }

            string currentClipboardText = await Task.Run(async () =>
            {
                try
                {
                    string text = await ClipboardData.GetTextAsync() as string;
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

            string currentClipboardText = Task.Run(async () =>
            {
                string text = await clipboardData.GetTextAsync() as string;
                return text;
            }).Result;

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
