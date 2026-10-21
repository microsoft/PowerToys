// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class TextExpanderPage : NavigablePage, IRefreshablePage, INotifyPropertyChanged
    {
        private readonly TextExpanderReplacementEditor _replacementEditor;
        private readonly AccessibilitySettings _accessibilitySettings = new();
        private readonly UISettings _uiSettings = new();
        private readonly List<(MenuFlyoutItem Item, TextExpanderVariable Variable)> _variableItems = new();
        private readonly List<(MenuFlyoutItem Item, string Format)> _datePresetItems = new();
        private readonly List<(Button Chip, TextBlock Example, (string Code, string MeaningKey, string ExampleKey) Code)> _dateCodeChips = new();
        private int? _editingId;
        private string _snippetProblem;
        private bool _isSnippetValid;
        private Control _lastEditedField;
        private TextExpanderSnippetViewModel _originalSnippet;
        private bool _allowDialogClose;
        private bool _showDiscardWarning;
        private int _editingRevision;
        private bool _keycapColumnUpdateQueued;
        private TextBlock _triggerMeasure;
        private (int Start, int End) _promptInsertionSelection;

        public TextExpanderPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new TextExpanderViewModel(settingsUtils, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, PickSingleFolderDialog, App.IsElevated);
            DataContext = ViewModel;
            this.InitializeComponent();
            InsetLikeAnInfoBarMessage(PrivacyNoticeBullets);
            BuildVariableMenu();
            BuildDateFormatGuide();
            _replacementEditor = new TextExpanderReplacementEditor(ReplacementBox, GetVariableColors);
            _replacementEditor.TextChanged += (_, _) => Validate();

            // The guide follows the caret into and out of {{date:…}} variables, and what it shows follows the typing.
            _replacementEditor.TextChanged += (_, _) => UpdateDateFormatGuide();
            _replacementEditor.SelectionChanged += (_, _) => UpdateDateFormatGuide();

            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // While shown, the list picks up the starter snippets the engine writes when it first starts.
            Loaded += (_, _) => ViewModel.Dispatch = action => dispatcher.TryEnqueue(() => action());
            Unloaded += (_, _) => ViewModel.Dispatch = null;

            // Only while the page is shown, so Windows doesn't keep a closed page alive.
            Loaded += (_, _) => _uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;
            Unloaded += (_, _) => _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
            SnippetDialog.Opened += (_, _) => _replacementEditor.Recolor();

            // The keycap column fits the triggers on show, so it follows the list and the search.
            ViewModel.FilteredSnippets.CollectionChanged += (_, _) => QueueKeycapColumnUpdate();
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.CaseSensitive))
                {
                    QueueKeycapColumnUpdate();
                }
            };
            QueueKeycapColumnUpdate();

            // The editor's discard prompt gets the same inset as Good to know.
            InsetLikeAnInfoBarMessage(DiscardWarningButtons);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Opens the Windows folder picker, as Keyboard Manager's editor does, at
        /// <paramref name="startFolder"/> when it exists. Returns null when cancelled.
        ///
        /// <para>
        /// The General page keeps the older shell32 folder browser because the WinRT picker
        /// fails when Settings runs elevated. Neither applies here: this Windows App SDK picker
        /// works elevated, and snippet editing, the only thing that opens it, is off while
        /// Settings is elevated.
        /// </para>
        /// </summary>
        private async Task<string> PickSingleFolderDialog(string startFolder)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.GetSettingsWindow());
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
            {
                // Every time, unlike SuggestedStartFolder, which counts only until the first pick.
                picker.SuggestedFolder = startFolder;
            }

            var result = await picker.PickSingleFolderAsync();
            return result?.Path;
        }

        /// <summary>Gets the reason the snippet cannot be saved, or null.</summary>
        public string SnippetProblem
        {
            get => _snippetProblem;
            private set
            {
                if (Set(ref _snippetProblem, value))
                {
                    OnPropertyChanged(nameof(HasSnippetProblem));
                }
            }
        }

        public bool HasSnippetProblem => !string.IsNullOrEmpty(_snippetProblem);

        /// <summary>Gets a value indicating whether Save is allowed.</summary>
        public bool IsSnippetValid
        {
            get => _isSnippetValid;
            private set => Set(ref _isSnippetValid, value);
        }

        public bool ShowDiscardWarning
        {
            get => _showDiscardWarning;
            private set => Set(ref _showDiscardWarning, value);
        }

        private TextExpanderViewModel ViewModel { get; set; }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        /// <summary>
        /// Insets content in an InfoBar the way its template insets its own message, which custom
        /// content doesn't get, so text would run into the InfoBar's right edge: by
        /// InfoBarPanelMargin on the right, and below by the padding under a message that wraps,
        /// InfoBarPanelVerticalOrientationPadding. Both come from WinUI's theme resources.
        /// </summary>
        private static void InsetLikeAnInfoBarMessage(FrameworkElement content)
            => content.Margin = new Thickness(0, 0, ThemeThickness("InfoBarPanelMargin").Right, ThemeThickness("InfoBarPanelVerticalOrientationPadding").Bottom);

        private static Thickness ThemeThickness(string key)
            => Application.Current.Resources.TryGetValue(key, out object value) && value is Thickness thickness ? thickness : default;

        /// <summary>
        /// Builds the Insert variable menu from the view model, so the menu cannot drift from the
        /// set of variables the engine can actually expand. Its samples are filled in each time
        /// it opens, so the time and date are current.
        /// </summary>
        private void BuildVariableMenu()
        {
            foreach (TextExpanderVariable variable in ViewModel.Variables)
            {
                if (variable.IsCustomDate)
                {
                    VariableFlyout.Items.Add(BuildDateFormatMenu(variable));
                    continue;
                }

                var item = new MenuFlyoutItem { Tag = variable.Token };
                item.Click += variable.IsPrompt ? ConfigurePromptVariable_Click : InsertVariable_Click;
                VariableFlyout.Items.Add(item);
                _variableItems.Add((item, variable));
            }

            RefreshVariableMenu(DateTime.Now);
            VariableFlyout.Opening += (_, _) => RefreshVariableMenu(DateTime.Now);
        }

        /// <summary>
        /// Builds Custom date format's submenu: ready-made formats, each shown as it comes out now,
        /// with its code in its tooltip and name; then Custom format…, which inserts one to edit
        /// with the date format guide.
        /// </summary>
        private MenuFlyoutSubItem BuildDateFormatMenu(TextExpanderVariable custom)
        {
            var menu = new MenuFlyoutSubItem { Text = custom.Name };
            foreach (string format in TextExpanderDateGuide.Presets)
            {
                string token = TextExpanderDateGuide.Token(format);
                var item = new MenuFlyoutItem { Tag = token };
                ToolTipService.SetToolTip(item, token);
                item.Click += InsertDatePreset_Click;
                menu.Items.Add(item);
                _datePresetItems.Add((item, format));
            }

            menu.Items.Add(new MenuFlyoutSeparator());
            var customFormat = new MenuFlyoutItem
            {
                Text = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_DateFormats_Custom"),
                Tag = custom.Token,
            };
            customFormat.Click += InsertVariable_Click;
            menu.Items.Add(customFormat);
            return menu;
        }

        private void RefreshVariableMenu(DateTime now)
        {
            foreach ((MenuFlyoutItem item, TextExpanderVariable variable) in _variableItems)
            {
                item.Text = ViewModel.VariableMenuText(variable, now);
            }

            foreach ((MenuFlyoutItem item, string format) in _datePresetItems)
            {
                item.Text = TextExpanderDateGuide.Example(format, now);
                AutomationProperties.SetName(item, TextExpanderDateGuide.PresetName(format, now, ResourceLoaderInstance.ResourceLoader.GetString));
            }
        }

        /// <summary>
        /// Fills the date format guide with its codes, a row each for years, months, days and
        /// times. Each chip shows its code and what it gives, and puts its code in at the caret.
        /// </summary>
        private void BuildDateFormatGuide()
        {
            foreach (IReadOnlyList<(string Code, string MeaningKey, string ExampleKey)> row in TextExpanderDateGuide.Rows)
            {
                var line = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 4, VerticalSpacing = 4 };
                foreach ((string Code, string MeaningKey, string ExampleKey) code in row)
                {
                    var example = new TextBlock { Style = (Style)Resources["TextExpanderDateExampleStyle"] };
                    var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    content.Children.Add(new TextBlock { Style = (Style)Resources["TextExpanderDateCodeStyle"], Text = code.Code });
                    content.Children.Add(example);
                    var chip = new Button { Content = content, Padding = new Thickness(6, 2, 6, 3), Tag = code.Code };
                    ToolTipService.SetToolTip(chip, ResourceLoaderInstance.ResourceLoader.GetString(code.MeaningKey));
                    chip.Click += DateCode_Click;
                    line.Children.Add(chip);
                    _dateCodeChips.Add((chip, example, code));
                }

                DateFormatCodes.Children.Add(line);
            }
        }

        /// <summary>
        /// Shows the date format guide while the caret is in a {{date:…}} variable, with each code's
        /// example and what the format shows, as of now. Hidden anywhere else.
        /// </summary>
        private void UpdateDateFormatGuide()
        {
            (int start, int end) = _replacementEditor.Selection;
            if (TextExpanderDateGuide.TokenAround(_replacementEditor.Text, start, end) is not { } token)
            {
                DateFormatGuide.Visibility = Visibility.Collapsed;
                return;
            }

            DateTime now = DateTime.Now;
            Func<string, string> resource = ResourceLoaderInstance.ResourceLoader.GetString;
            foreach ((Button chip, TextBlock example, (string Code, string MeaningKey, string ExampleKey) code) in _dateCodeChips)
            {
                example.Text = TextExpanderDateGuide.CodeExample(code, now, resource);
                AutomationProperties.SetName(chip, TextExpanderDateGuide.CodeName(code, now, resource));
            }

            DateFormatShowsAs.Text = TextExpanderDateGuide.ShowsAs(token.Format, now, resource);
            DateFormatShowsAs.Visibility = DateFormatShowsAs.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            DateFormatGuide.Visibility = Visibility.Visible;
        }

        private void DateCode_Click(object sender, RoutedEventArgs e)
        {
            // Back to the box afterwards, to go on typing the format.
            if (sender is Button { Tag: string code } && _replacementEditor.InsertDateCode(code))
            {
                FocusAfterLayout(ReplacementBox);
            }
        }

        private void InsertDatePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { Tag: string token })
            {
                return;
            }

            // A ready-made format is finished, so the caret goes after it rather than selecting it.
            _replacementEditor.InsertVariable(token, selectEditablePart: false);
            FocusAfterLayout(ReplacementBox);
        }

        private void InsertVariable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.Tag is not string token)
            {
                return;
            }

            // Insert at the caret rather than appending, so a variable can go in the middle of a
            // sentence -- which is where most of them belong.
            _replacementEditor.InsertVariable(token);

            // Once the menu has closed, which hands focus back to its button.
            FocusAfterLayout(ReplacementBox);
        }

        private void ConfigurePromptVariable_Click(object sender, RoutedEventArgs e)
        {
            _promptInsertionSelection = _replacementEditor.Selection;
            PromptQuestionBox.Text = string.Empty;
            PromptRequireAnswer.IsChecked = true;
            PromptQuestionError.Visibility = Visibility.Collapsed;
            PromptVariableBuilder.Visibility = Visibility.Visible;
            FocusAfterLayout(PromptQuestionBox);
        }

        private void PromptQuestionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string question = PromptQuestionBox.Text?.Trim() ?? string.Empty;
            bool hasClosingBrace = question.Contains('}');
            InsertPromptVariableButton.IsEnabled = question.Length > 0 && !hasClosingBrace;

            if (hasClosingBrace)
            {
                PromptQuestionError.Text = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_PromptBuilder_InvalidQuestion");
                PromptQuestionError.Visibility = Visibility.Visible;
            }
            else
            {
                PromptQuestionError.Visibility = Visibility.Collapsed;
            }
        }

        private void PromptQuestionBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                InsertPromptVariable_Click(sender, e);
            }
        }

        private void InsertPromptVariable_Click(object sender, RoutedEventArgs e)
        {
            string question = PromptQuestionBox.Text?.Trim() ?? string.Empty;
            if (question.Length == 0 || question.Contains('}'))
            {
                PromptQuestionError.Text = ResourceLoaderInstance.ResourceLoader.GetString(
                    question.Length == 0 ? "TextExpander_PromptBuilder_QuestionRequired" : "TextExpander_PromptBuilder_InvalidQuestion");
                PromptQuestionError.Visibility = Visibility.Visible;
                FocusAfterLayout(PromptQuestionBox);
                return;
            }

            string token = string.Format(
                CultureInfo.InvariantCulture,
                PromptRequireAnswer.IsChecked == false ? "{{{{prompt?:{0}}}}}" : "{{{{prompt:{0}}}}}",
                question);
            _replacementEditor.InsertVariableAt(token, _promptInsertionSelection.Start, _promptInsertionSelection.End, selectEditablePart: false);
            PromptVariableBuilder.Visibility = Visibility.Collapsed;
            FocusAfterLayout(ReplacementBox);
        }

        private void CancelPromptVariable_Click(object sender, RoutedEventArgs e)
        {
            PromptVariableBuilder.Visibility = Visibility.Collapsed;
            FocusAfterLayout(ReplacementBox);
        }

        private async void AddSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            await ShowSnippetDialogAsync(null, string.Empty, string.Empty);
        }

        private async void StarterSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            var resources = ResourceLoaderInstance.ResourceLoader;
            if (sender is not FrameworkElement { Tag: string trigger })
            {
                return;
            }

            string replacement = trigger switch
            {
                "!mysig" => resources.GetString("TextExpander_StarterSignature_Text"),
                "!myemail" => "you@example.com",
                "!isodate" => "{{isodate}}",
                _ => throw new ArgumentException("Unknown starter snippet.", nameof(sender)),
            };

            await ShowSnippetDialogAsync(null, trigger, replacement);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SearchText = string.Empty;
            SnippetSearchBox.Focus(FocusState.Programmatic);
        }

        private void ReloadSnippets_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadSnippets();
            if (!ViewModel.HasSnippetError)
            {
                SnippetStatus.Text = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_Reloaded");
                Announce(SnippetStatus, "textExpanderSnippetFileReloaded");
            }
        }

        private async void EditSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            // Both the row and its menu's Edit come here.
            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            if (snippet != null)
            {
                await ShowSnippetDialogAsync(snippet.Id, snippet.Trigger, snippet.Replacement);
            }
        }

        private async void DuplicateSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            if (snippet == null)
            {
                return;
            }

            // The copy opens in the editor rather than being saved straight away, so it gets a
            // trigger of its own first; nothing is written until Save. The suggested trigger is
            // one that can fire, unlike the original's name plus a number.
            await ShowSnippetDialogAsync(null, ViewModel.SuggestDuplicateTrigger(snippet.Id) ?? string.Empty, snippet.Replacement);
        }

        private async void DeleteSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            if (snippet == null)
            {
                return;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            // Confirm, because there is no undo and the file is the only copy.
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_Title"),
                Content = string.Format(
                    CultureInfo.CurrentCulture,
                    resourceLoader.GetString("TextExpander_DeleteSnippetDialog_Content"),
                    snippet.Trigger),
                PrimaryButtonText = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_PrimaryButtonText"),
                CloseButtonText = resourceLoader.GetString("TextExpander_DeleteSnippetDialog_CloseButtonText"),
                DefaultButton = ContentDialogButton.Close,
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            string error = ViewModel.DeleteSnippet(snippet.Id);
            if (error != null)
            {
                await ShowErrorAsync(error);
            }
            else
            {
                AnnounceSnippetChange("TextExpander_SnippetDeleted");
            }
        }

        /// <summary>
        /// Gets the snippet a row, or an item in its menu, stands for. The list is an
        /// ItemsRepeater, which leaves DataContext unset on rows built with x:Bind, so every
        /// clickable part of a row carries its snippet in Tag="{x:Bind}".
        /// </summary>
        private static TextExpanderSnippetViewModel GetSnippetFromSender(object sender)
            => sender is FrameworkElement element ? TextExpanderRowSnippet.Resolve(element.Tag, element.DataContext) : null;

        private async System.Threading.Tasks.Task ShowSnippetDialogAsync(int? id, string trigger, string replacement)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            _editingId = id;
            _editingRevision = ViewModel.SnippetRevision;
            _originalSnippet = new TextExpanderSnippetViewModel(id ?? -1, trigger ?? string.Empty, replacement ?? string.Empty);
            _allowDialogClose = false;
            ShowDiscardWarning = false;
            _lastEditedField = TriggerBox;
            PromptVariableBuilder.Visibility = Visibility.Collapsed;
            SnippetStatus.Text = string.Empty;
            TriggerBox.Text = trigger ?? string.Empty;
            _replacementEditor.SetText(replacement ?? string.Empty);

            SnippetDialog.XamlRoot = XamlRoot;
            Validate();

            if (await SnippetDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AnnounceSnippetChange("TextExpander_SnippetSaved");
            }
        }

        private void SnippetDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Validate();
            if (!IsSnippetValid)
            {
                args.Cancel = true;
                return;
            }

            string error = ViewModel.SaveSnippet(_editingId, TriggerBox.Text, _replacementEditor.Text);
            if (error != null)
            {
                args.Cancel = true;
                Validate();
                if (IsSnippetValid)
                {
                    SnippetProblem = error;
                }

                return;
            }

            _allowDialogClose = true;
        }

        private void SnippetDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (!_allowDialogClose && _originalSnippet.HasChanges(TriggerBox.Text, _replacementEditor.Text))
            {
                args.Cancel = true;
                ShowDiscardWarning = true;
                FocusWhenShown(KeepEditingButton, DiscardWarning, () => Announce(DiscardWarning, DiscardWarning.Title, "textExpanderDiscardWarning"));
            }
        }

        private void KeepEditing_Click(object sender, RoutedEventArgs e)
        {
            ShowDiscardWarning = false;

            // Back to whichever field the user was working in.
            (_lastEditedField ?? TriggerBox).Focus(FocusState.Programmatic);
        }

        private void SnippetField_GotFocus(object sender, RoutedEventArgs e) => _lastEditedField = (Control)sender;

        private void DiscardChanges_Click(object sender, RoutedEventArgs e)
        {
            _allowDialogClose = true;
            SnippetDialog.Hide();
        }

        private void AnnounceSnippetChange(string resourceKey)
        {
            SnippetStatus.Text = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            if (ViewModel.HasSearchText)
            {
                SnippetStatus.Text += " " + ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SearchStillActive");
            }

            Announce(SnippetStatus, "textExpanderSnippetChanged");
        }

        private static void Announce(TextBlock status, string activityId) => Announce(status, status.Text, activityId);

        private static void Announce(UIElement element, string text, string activityId)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(element)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                text,
                activityId);
        }

        private void FocusAfterLayout(Control control)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => control.Focus(FocusState.Programmatic));
        }

        /// <summary>Recomputes the keycap column once, after a burst of list changes such as a reload or a search.</summary>
        private void QueueKeycapColumnUpdate()
        {
            if (_keycapColumnUpdateQueued)
            {
                return;
            }

            _keycapColumnUpdateQueued = true;
            DispatcherQueue.TryEnqueue(() =>
            {
                _keycapColumnUpdateQueued = false;

                // Only the triggers that can be widest are measured, not every row.
                ViewModel.KeycapColumnWidth = TextExpanderKeycapLayout.ColumnWidth(
                    TextExpanderKeycapLayout.WidestTriggers(ViewModel.FilteredSnippets.Select(snippet => snippet.TriggerDisplay)).Select(MeasureTrigger));
            });
        }

        /// <summary>Measures a trigger in the keycap's font, which the column has to fit.</summary>
        private double MeasureTrigger(string trigger)
        {
            _triggerMeasure ??= new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            _triggerMeasure.Text = trigger ?? string.Empty;
            _triggerMeasure.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            return _triggerMeasure.DesiredSize.Width;
        }

        /// <summary>
        /// Focuses a control that is only now being shown. Inside a collapsed InfoBar it does not
        /// exist until the InfoBar's template is applied, so wait for it to load.
        /// <paramref name="bringIntoView"/>, if given, is scrolled into view instead of the
        /// control, and <paramref name="shown"/> runs once the control has focus.
        /// </summary>
        private void FocusWhenShown(Control control, FrameworkElement bringIntoView = null, Action shown = null)
        {
            void Focus()
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    (bringIntoView ?? control).StartBringIntoView();
                    control.Focus(FocusState.Programmatic);
                    shown?.Invoke();
                });
            }

            if (control.IsLoaded)
            {
                Focus();
                return;
            }

            void OnLoaded(object sender, RoutedEventArgs e)
            {
                control.Loaded -= OnLoaded;
                Focus();
            }

            control.Loaded += OnLoaded;
        }

        private async void ChooseSnippetsFolder_Click(object sender, RoutedEventArgs e)
        {
            TextExpanderFolderChange change = await ViewModel.PickSnippetsFolderAsync();
            if (change != null)
            {
                await ApplyFolderChangeAsync(change);
            }
        }

        private async Task ApplyFolderChangeAsync(TextExpanderFolderChange change)
        {
            TextExpanderFolderConflictChoice choice = TextExpanderFolderConflictChoice.ReplaceFolder;
            if (change.HasExistingFile)
            {
                TextExpanderFolderConflictChoice? selected = await ConfirmFolderConflictAsync(change);
                if (!selected.HasValue)
                {
                    return;
                }

                choice = selected.Value;
            }

            string error = ViewModel.ApplyFolderChange(change, choice, out string backup);
            if (error != null)
            {
                await ShowStorageErrorAsync(error);
                return;
            }

            ReportStorageChange(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_Changed"),
                    ViewModel.SnippetsFilePath),
                backup);
        }

        private async Task<TextExpanderFolderConflictChoice?> ConfirmFolderConflictAsync(TextExpanderFolderChange change)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("TextExpander_SnippetFolder_Conflict_Title"),
                Content = new TextBlock
                {
                    Text = string.Format(
                        CultureInfo.CurrentCulture,
                        resources.GetString("TextExpander_SnippetFolder_Conflict_Message"),
                        change.FilePath),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
                PrimaryButtonText = resources.GetString("TextExpander_SnippetFolder_Conflict_Use"),
                SecondaryButtonText = resources.GetString("TextExpander_SnippetFolder_Conflict_Replace"),
                CloseButtonText = resources.GetString("TextExpander_Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };

            return await confirm.ShowAsync() switch
            {
                ContentDialogResult.Primary => TextExpanderFolderConflictChoice.UseFolder,
                ContentDialogResult.Secondary => TextExpanderFolderConflictChoice.ReplaceFolder,
                _ => null,
            };
        }

        private async void ClearSnippets_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanClearSnippets)
            {
                return;
            }

            var resources = ResourceLoaderInstance.ResourceLoader;
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("TextExpander_SnippetFolder_ClearDialog_Title"),
                Content = string.Format(
                    CultureInfo.CurrentCulture,
                    resources.GetString("TextExpander_SnippetFolder_ClearDialog_Message"),
                    ViewModel.SnippetsFilePath),
                PrimaryButtonText = resources.GetString("TextExpander_SnippetFolder_ClearDialog_PrimaryButtonText"),
                CloseButtonText = resources.GetString("TextExpander_Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            string error = ViewModel.ClearSnippets(out string backup);
            if (error != null)
            {
                await ShowStorageErrorAsync(error);
                return;
            }

            ReportStorageChange(resources.GetString("TextExpander_SnippetFolder_Cleared"), backup);
        }

        private void ReportStorageChange(string message, string backup)
        {
            if (!string.IsNullOrEmpty(backup))
            {
                message += " " + string.Format(
                    CultureInfo.CurrentCulture,
                    ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_BackupCreated"),
                    backup);
            }

            SnippetStatus.Text = message;
            Announce(SnippetStatus, "textExpanderSnippetStorageChanged");
        }

        private async Task ShowStorageErrorAsync(string error)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("TextExpander_SnippetFolder_Error_Title"),
                Content = error,
                CloseButtonText = resources.GetString("TextExpander_Dialog_Close"),
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }

        private void SnippetField_Changed(object sender, TextChangedEventArgs e) => Validate();

        private void Validate()
        {
            string trigger = TriggerBox.Text ?? string.Empty;
            string replacement = _replacementEditor.Text;

            ShowPreview(replacement);

            if (_editingId.HasValue && _editingRevision != ViewModel.SnippetRevision)
            {
                SnippetProblem = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_EditorFileChanged");
                IsSnippetValid = false;
                return;
            }

            // Quiet while the dialog is still empty, but once there's text a missing trigger
            // says why Save is off.
            string problem = ViewModel.ValidateSnippet(_editingId, trigger, replacement);
            SnippetProblem = trigger.Length == 0 && replacement.Length == 0 ? null : problem;
            IsSnippetValid = problem == null;
        }

        /// <summary>
        /// Shows what the snippet types, with a bubble for each value that is only known when it
        /// runs: the clipboard, an answer, or a date or time (shown as it is now).
        /// </summary>
        private void ShowPreview(string replacement)
        {
            IReadOnlyList<TextExpanderPreviewSegment> segments = TextExpanderSnippetPreview.Build(replacement, DateTime.Now);
            SnippetPreviewText.Segments = segments;

            // Screen readers do not reliably reach elements inside text, so the preview's name
            // spells out the bubbles too.
            SnippetPreviewText.AccessibleName = TextExpanderSnippetPreview.Describe(segments, TextExpanderBubbleText.DescribeBubble);
            PreviewHint.Visibility = TextExpanderSnippetPreview.HasBubbles(segments) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Gets the colors for variables in the replacement box: accent text on a faint accent
        /// tint, from the current theme. RichEdit draws opaque colors, so the tint is mixed here
        /// over the box's own background. None in high contrast, where the system colors apply.
        /// </summary>
        private (Color Text, Color Background)? GetVariableColors()
        {
            if (VariableColorProbe.BorderBrush is not SolidColorBrush accent
                || VariableColorProbe.Background is not SolidColorBrush box
                || VariableColorProbeBase.Background is not SolidColorBrush page)
            {
                return null;
            }

            return TextExpanderEditorColors.Variables(_accessibilitySettings.HighContrast, accent.Color, box.Color, page.Color);
        }

        /// <summary>
        /// Recolors the replacement box when Windows' colors change, which includes turning high
        /// contrast on or off. Raised off the UI thread.
        /// </summary>
        private void UISettings_ColorValuesChanged(UISettings sender, object args)
            => DispatcherQueue.TryEnqueue(() => _replacementEditor.Recolor());

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString("TextExpander_SaveSnippetsErrorDialog_Title"),
                Content = message,
                CloseButtonText = resourceLoader.GetString("TextExpander_SaveSnippetsErrorDialog_CloseButtonText"),
            };

            await dialog.ShowAsync();
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
