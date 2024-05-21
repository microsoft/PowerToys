// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;
using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Settings;
using Common.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinUIEx;
using static AdvancedPaste.Helpers.NativeMethods;
using Application = Microsoft.UI.Xaml.Application;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using BitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;
using Clipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.ViewModels
{
    public partial class OptionsViewModel : ObservableObject
    {
        internal struct SavedClipboardItem
        {
            public ClipboardHelper.ClipboardContentFormats Format { get; set; }

            public string Text { get; set; }

            public string HTML { get; set; }

            public string Filename { get; set; }

            public SoftwareBitmap Image { get; set; }
        }

        private static readonly string[] FunctionNames =
       {
        "ToCustomWithAI",
        "RemoveBackground",
        "ToJSON",
        "ToPlainText",
        "ToMarkdown",
        "ToFile",
        "AudioToText",
       };

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private App app = App.Current as App;

        private AICompletionsHelper aiHelper;

        private UserSettings _userSettings;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxPlaceholderText))]
        private bool _isCustomAIEnabled;

        [ObservableProperty]
        private bool _clipboardHistoryEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InputTxtBoxErrorText))]
        private int _apiRequestStatus;

        [ObservableProperty]
        private string _customFormatResult;

        [ObservableProperty]
        private bool _customFormatIsHTML;

        [ObservableProperty]
        private DataPackageView _clipboardContent;

        [ObservableProperty]
        private bool _clipboardHasText;

        [ObservableProperty]
        private bool _clipboardHasHtml;

        [ObservableProperty]
        private bool _clipboardHasImage;

        [ObservableProperty]
        private bool _clipboardHasFile;

        [ObservableProperty]
        private bool _clipboardHasAudio;

        // List to store generated responses
        internal ObservableCollection<SavedClipboardItem> GeneratedResponses { get; set; } = new ObservableCollection<SavedClipboardItem>();

        // Index to keep track of the current response
        private int _currentResponseIndex;

        internal int CurrentResponseIndex
        {
            get => _currentResponseIndex;
            set
            {
                if (value >= 0 && value < GeneratedResponses.Count)
                {
                    SetProperty(ref _currentResponseIndex, value);
                    CustomFormatResult = GeneratedResponses[_currentResponseIndex].Text;
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

                if (!aiHelper.IsAIEnabled)
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("OpenAINotConfigured");
                }
                else
                {
                    return ResourceLoaderInstance.ResourceLoader.GetString("CustomFormatTextBox/PlaceholderText");
                }
            }
        }

        public string InputTxtBoxErrorText
        {
            get
            {
                if (ApiRequestStatus != (int)HttpStatusCode.OK)
                {
                    if (ApiRequestStatus == (int)HttpStatusCode.TooManyRequests)
                    {
                        return ResourceLoaderInstance.ResourceLoader.GetString("OpenAIAPIKeyTooManyRequests");
                    }
                    else if (ApiRequestStatus == (int)HttpStatusCode.Unauthorized)
                    {
                        return ResourceLoaderInstance.ResourceLoader.GetString("OpenAIAPIKeyUnauthorized");
                    }
                    else
                    {
                        return ResourceLoaderInstance.ResourceLoader.GetString("OpenAIAPIKeyError") + ApiRequestStatus.ToString(CultureInfo.InvariantCulture);
                    }
                }

                return string.Empty;
            }
        }

        private AILocalModelsHelper aiLocalModelsHelper;

        public event Func<string, bool> FormatsChanged;

        public event Func<bool> WindowShown;

        public OptionsViewModel()
        {
            aiHelper = new AICompletionsHelper();
            _userSettings = new UserSettings();

            IsCustomAIEnabled = aiHelper.IsAIEnabled;

            ApiRequestStatus = (int)HttpStatusCode.OK;

            GeneratedResponses = new ObservableCollection<SavedClipboardItem>();
            GeneratedResponses.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasMultipleResponses));
                OnPropertyChanged(nameof(CurrentIndexDisplay));
            };

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            aiLocalModelsHelper = new AILocalModelsHelper();
        }

        public void GetClipboardData()
        {
            ClipboardContent = Clipboard.GetContent();

            ClipboardHasText = false;
            ClipboardHasHtml = false;
            ClipboardHasImage = false;
            ClipboardHasFile = false;
            ClipboardHasAudio = false;

            if (ClipboardContent == null)
            {
                Logger.LogWarning("Clipboard does not contain any data");
                return;
            }

            if (ClipboardContent.Contains(StandardDataFormats.Text))
            {
                ClipboardHasText = true;
            }

            if (ClipboardContent.Contains(StandardDataFormats.Html))
            {
                ClipboardHasHtml = true;
            }

            if (ClipboardContent.Contains(StandardDataFormats.Bitmap))
            {
                ClipboardHasImage = true;
            }

            if (ClipboardContent.Contains(StandardDataFormats.StorageItems))
            {
                // Get storage items and iterate through their file names to find endings
                // to enable audio and image to text
                ClipboardHasFile = true;
                try
                {
                    var storageItemsAwaiter = ClipboardContent.GetStorageItemsAsync();
                    storageItemsAwaiter.AsTask().Wait();
                    var storageItems = storageItemsAwaiter.GetResults();
                    foreach (var storageItem in storageItems)
                    {
                        if (storageItem is Windows.Storage.StorageFile file)
                        {
                            if (file.ContentType.Contains("audio") || file.Name.EndsWith("waptt", StringComparison.InvariantCulture))
                            {
                                if (file.ContentType.Contains("audio"))
                                {
                                    ClipboardHasAudio = true;
                                }
                                else if (file.ContentType.Contains("image"))
                                {
                                    ClipboardHasImage = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("Error getting storage items", e);
                }
            }
        }

        public void OnShow()
        {
            GetClipboardData();

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
                        IsCustomAIEnabled = aiHelper.IsAIEnabled;
                    });
                },
                    TaskScheduler.Default);
            }
            else
            {
                IsCustomAIEnabled = aiHelper.IsAIEnabled;
            }

            ClipboardHistoryEnabled = IsClipboardHistoryEnabled();
            GeneratedResponses.Clear();
            WindowShown?.Invoke();
        }

        private void HideWindow()
        {
            if (app.GetMainWindow() != null)
            {
                Windows.Win32.Foundation.HWND hwnd = (Windows.Win32.Foundation.HWND)app.GetMainWindow().GetWindowHandle();
                Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
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

        [RelayCommand]
        public void PasteCustom()
        {
            _ = PasteCustomFunction(GeneratedResponses[CurrentResponseIndex]);
        }

        private void SetClipboardContentAndHideWindow(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                ClipboardHelper.SetClipboardTextContent(content);
            }

            HideWindow();
        }

        internal async void ToPlainText()
        {
            try
            {
                Logger.LogTrace();

                string outputString = await ClipboardHelper.GetClipboardTextContent(ClipboardContent);

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

        internal async void ToMarkdown(bool pasteAlways = false)
        {
            try
            {
                Logger.LogTrace();

                string inputString = string.Empty;

                if (ClipboardHasHtml)
                {
                    inputString = await ClipboardHelper.GetClipboardHTMLContent(ClipboardContent);
                }
                else if (ClipboardHasText)
                {
                    inputString = await ClipboardHelper.GetClipboardTextContent(ClipboardContent);
                }

                string outputString = ToMarkdownFunction(inputString);

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

        internal string ToMarkdownFunction(string inputHTML)
        {
            return MarkdownHelper.ConvertHtmlToMarkdown(inputHTML);
        }

        internal async void ToJson(bool pasteAlways = false)
        {
            try
            {
                Logger.LogTrace();

                string inputText = await ClipboardHelper.GetClipboardTextContent(ClipboardContent);

                string jsonText = ToJsonFunction(inputText);

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

        internal string ToJsonFunction(string inputString, bool pasteAlways = false)
        {
            return JsonHelper.ToJsonFromXmlOrCsv(inputString);
        }

        internal async void AudioToText()
        {
            try
            {
                Logger.LogTrace();

                var fileContent = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                string outputText = await AudioToTextFunction(fileContent);
                ClipboardHelper.SetClipboardTextContent(outputText);

                SetClipboardContentAndHideWindow(outputText);

                if (_userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }
        }

        internal async Task<string> AudioToTextFunction(DataPackageView fileContent)
        {
            var fileList = await fileContent.GetStorageItemsAsync();
            var outputText = string.Empty;
            StorageFile file = null;
            if (fileList.Count > 0)
            {
                file = fileList[0] as StorageFile;

                outputText = await aiLocalModelsHelper.DoWhisperInference(file);

                return outputText;
            }
            else
            {
                // TODO: Add error handling
                Console.WriteLine("Hit error");
                return string.Empty;
            }
        }

        internal async Task<string> AudioToTextFunction(string fileName, int startSeconds, int durationSeconds)
        {
            // Get StorageFile from fileName
            var file = await StorageFile.GetFileFromPathAsync(fileName);

            var outputText = await aiLocalModelsHelper.DoWhisperInference(file, startSeconds, durationSeconds);

            return outputText;
        }

        internal async Task<string> CustomWithAIFunction(string inputInstructions, string inputContent)
        {
            var aiOutput = await Task.Run(() => aiHelper.AIFormatString(inputInstructions, inputContent));

            return aiOutput.Response;
        }

        internal async void ToFile()
        {
            try
            {
                Logger.LogTrace();

                // Determine the type of content in the clipboard
                string fileName = null;

                if (ClipboardHasText)
                {
                    string clipboardText = await ClipboardContent.GetTextAsync();
                    fileName = await ToFileFunction(clipboardText);
                }
                else if (ClipboardHasImage)
                {
                    SoftwareBitmap softwareBitmap = await ClipboardHelper.GetClipboardImageContent(ClipboardContent);
                    fileName = await ToFileFunction(softwareBitmap);
                }

                // Set the clipboard data
                _ = await ClipboardHelper.SetClipboardFile(fileName);
                HideWindow();

                if (_userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }
        }

        internal async Task<string> ToFileFunction(string inputContent)
        {
            // Create a local file in the temp directory
            string tempFileName = Path.Combine(Path.GetTempPath(), "clipboard.txt");

            // Write the content to the file
            await File.WriteAllTextAsync(tempFileName, inputContent);

            return tempFileName;
        }

        internal async Task<string> ToFileFunction(SoftwareBitmap softwareBitmap)
        {
            // Create a local file in the temp directory
            string tempFileName = Path.Combine(Path.GetTempPath(), "clipboard.png");

            using (var stream = new InMemoryRandomAccessStream())
            {
                // Encode the SoftwareBitmap to the stream
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                await encoder.FlushAsync();

                // Set the stream position to the beginning
                stream.Seek(0);

                // Create a new file in the temporary directory with a .png extension
                using (var fileStream = File.Create(tempFileName))
                {
                    await stream.AsStream().CopyToAsync(fileStream);
                }
            }

            return tempFileName;
        }

        internal async Task<string> GenerateCustomFunction(string inputInstructions)
        {
            Logger.LogTrace();

            // Get what operations are needed from the AI
            // For whatever operation is returned do that
            string aiOperationsOutput = await Task.Run(() => aiHelper.GetOperationsFromAI(inputInstructions, ClipboardHasText, ClipboardHasImage, ClipboardHasHtml, ClipboardHasFile, ClipboardHasAudio));

            // Define in loop variables to hold values
            string currentClipboardText = await ClipboardHelper.GetClipboardTextContent(ClipboardContent);
            string currentClipboardHTML = await ClipboardHelper.GetClipboardHTMLContent(ClipboardContent);
            string currentFileName = await ClipboardHelper.GetClipboardFileName(ClipboardContent);
            SoftwareBitmap currentClipboardImage = null;

            if (ClipboardHasImage)
            {
                currentClipboardImage = await ClipboardHelper.GetClipboardImageContent(ClipboardContent);
            }

            ClipboardHelper.ClipboardContentFormats returnFormat = ClipboardHelper.ClipboardContentFormats.Invalid;

            string[] lines = aiOperationsOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                foreach (string functionName in OptionsViewModel.FunctionNames)
                {
                    if (line.Contains(functionName + "("))
                    {
                        switch (functionName)
                        {
                            case "ToCustomWithAI":
                                // Get the input instructions seen after 'CustomWithAI(' using regex to account for either the " or ' character
                                string pattern = @"CustomWithAI\(['""](.+?)['""]";

                                string customInputInstructions = string.Empty;

                                Match match = Regex.Match(line, pattern);
                                if (match.Success)
                                {
                                    customInputInstructions = match.Groups[1].Value;
                                }

                                string result = await CustomWithAIFunction(customInputInstructions, currentClipboardText);

                                currentClipboardHTML = result;
                                currentClipboardText = ClipboardHelper.ConvertHTMLToPlainText(currentClipboardHTML);
                                returnFormat = ClipboardHelper.ClipboardContentFormats.HTML;

                                break;
                            case "ToJSON":
                                break;
                            case "ToPlainText":
                                break;
                            case "ToMarkdown":
                                break;
                            case "ToFile":
                                if (currentClipboardText != null)
                                {
                                    currentFileName = await ToFileFunction(currentClipboardText);
                                }
                                else if (currentClipboardHTML != null)
                                {
                                    currentFileName = await ToFileFunction(currentClipboardHTML);
                                }
                                else if (currentClipboardImage != null)
                                {
                                    currentFileName = await ToFileFunction(currentClipboardImage);
                                }

                                returnFormat = ClipboardHelper.ClipboardContentFormats.File;

                                break;
                            case "AudioToText":
                                // Use regex and get the input instructions after AudioToText( and split them by the comma
                                string audioToTextPattern = @"AudioToText\((.+?)\)";

                                string audioToTextFileName = string.Empty;
                                int seekSeconds = 0;
                                int maxDurationSeconds = 0;

                                Match audioToTextMatch = Regex.Match(line, audioToTextPattern);
                                if (audioToTextMatch.Success)
                                {
                                    audioToTextFileName = audioToTextMatch.Groups[1].Value.Split(',')[0];
                                    seekSeconds = int.Parse(audioToTextMatch.Groups[1].Value.Split(',')[1], CultureInfo.InvariantCulture);
                                    maxDurationSeconds = int.Parse(audioToTextMatch.Groups[1].Value.Split(',')[2], CultureInfo.InvariantCulture);
                                }

                                currentClipboardText = await AudioToTextFunction(currentFileName, seekSeconds, maxDurationSeconds);
                                returnFormat = ClipboardHelper.ClipboardContentFormats.Text;
                                break;
                            default:
                                break;
                        }

                        break; // No need to check other function names for this line
                    }
                }
            }

            var resultSavedClipboardItem = new SavedClipboardItem
            {
                Format = returnFormat,
            };

            // DO return logic with enum
            switch (returnFormat)
            {
                case ClipboardHelper.ClipboardContentFormats.HTML:
                    resultSavedClipboardItem.HTML = currentClipboardHTML;
                    GeneratedResponses.Add(resultSavedClipboardItem);
                    CurrentResponseIndex = GeneratedResponses.Count - 1;
                    return currentClipboardHTML;

                // Other formats not yet supported
                case ClipboardHelper.ClipboardContentFormats.Image:
                    return "Image not implemented";
                case ClipboardHelper.ClipboardContentFormats.File:
                    resultSavedClipboardItem.Filename = currentFileName;
                    GeneratedResponses.Add(resultSavedClipboardItem);
                    CurrentResponseIndex = GeneratedResponses.Count - 1;
                    return "Paste as file.";
                case ClipboardHelper.ClipboardContentFormats.Audio:
                    return "Audio not implemented";
                case ClipboardHelper.ClipboardContentFormats.Text:
                    resultSavedClipboardItem.Text = currentClipboardText;
                    GeneratedResponses.Add(resultSavedClipboardItem);
                    CurrentResponseIndex = GeneratedResponses.Count - 1;
                    return currentClipboardText;
                default:
                    return string.Empty;
            }
        }

        internal async Task<bool> PasteCustomFunction(SavedClipboardItem inItem)
        {
            try
            {
                Logger.LogTrace();

                switch (inItem.Format)
                {
                    case ClipboardHelper.ClipboardContentFormats.HTML:
                        ClipboardHelper.SetClipboardHTMLContent(inItem.HTML);
                        break;
                    case ClipboardHelper.ClipboardContentFormats.Image:
                        break;
                    case ClipboardHelper.ClipboardContentFormats.File:
                        await ClipboardHelper.SetClipboardFile(inItem.Filename);
                        break;
                    case ClipboardHelper.ClipboardContentFormats.Audio:
                        break;
                    case ClipboardHelper.ClipboardContentFormats.Text:
                        ClipboardHelper.SetClipboardTextContent(inItem.Text);
                        break;
                    default:
                        break;
                }

                HideWindow();

                if (_userSettings.SendPasteKeyCombination)
                {
                    ClipboardHelper.SendPasteKeyCombination();
                }
            }
            catch
            {
            }

            return true;
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

        internal void FilterOptionsFromInput(string input)
        {
            // Generate event
            FormatsChanged?.Invoke(input);
        }
    }
}
