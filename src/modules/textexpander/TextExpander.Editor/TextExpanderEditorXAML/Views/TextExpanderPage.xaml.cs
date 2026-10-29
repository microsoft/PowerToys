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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Controls;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.TextExpander.Editor.Views
{
    public sealed partial class TextExpanderPage : Page, INotifyPropertyChanged
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
        private bool _isSnippetDialogOpen;
        private bool _keycapColumnUpdateQueued;
        private TextBlock _triggerMeasure;
        private TextExpanderPromptEditSession _promptEditSession;
        private PromptFlyoutCloseIntent _promptFlyoutCloseIntent;
        private MenuFlyout _rowMenu;
        private bool _closeWindowOnDiscard;

        private enum PromptFlyoutCloseIntent
        {
            None,
            Commit,
            Cancel,
        }

        public TextExpanderPage()
        {
            var settingsUtils = new TextExpanderSettingsUtils();
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            bool isElevated = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            ViewModel = new TextExpanderViewModel(settingsUtils, PickSingleFolderDialog, isElevated);
            DataContext = ViewModel;
            this.InitializeComponent();
            InsetLikeAnInfoBarMessage(PrivacyNoticeBullets);

            // An InfoBar announces itself as it opens only if it already has an automation peer, which nothing
            // makes for a collapsed one, so these two get theirs now.
            FrameworkElementAutomationPeer.CreatePeerForElement(SnippetErrorBar);
            FrameworkElementAutomationPeer.CreatePeerForElement(SnippetProblemBar);
            BuildVariableMenu();
            BuildDateFormatGuide();
            _replacementEditor = new TextExpanderReplacementEditor(ReplacementBox, GetVariableColors);
            _replacementEditor.TextChanged += (_, _) => Validate();
            _replacementEditor.PromptEditRequested += ReplacementEditor_PromptEditRequested;

            // The guide follows the caret into and out of {{date:…}} variables, and what it shows follows the typing.
            _replacementEditor.TextChanged += (_, _) => UpdateDateFormatGuide();
            _replacementEditor.SelectionChanged += (_, _) => UpdateDateFormatGuide();

            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            // While shown, the list picks up the starter snippets the engine writes when it first starts.
            Loaded += (_, _) =>
            {
                ViewModel.Dispatch = action => dispatcher.TryEnqueue(() => action());
                ViewModel.WatchSettings();
            };
            Unloaded += (_, _) => ViewModel.Dispose();

            // Only while the page is shown, so Windows doesn't keep a closed page alive.
            Loaded += (_, _) => _uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;
            Unloaded += (_, _) =>
            {
                _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                CancelPromptEdit();
            };
            SnippetDialog.Opened += (_, _) => _replacementEditor.Recolor();

            // The keycap column fits the triggers on show, so it follows the list and the search.
            ViewModel.FilteredSnippets.CollectionChanged += (_, _) => QueueKeycapColumnUpdate();
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.CaseSensitive))
                {
                    QueueKeycapColumnUpdate();
                }

                if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision))
                {
                    // A reload makes new rows, so an open More options menu would act on a row that's gone, or on
                    // another one once the list reuses its element.
                    _rowMenu?.Hide();
                }

                if ((e.PropertyName is nameof(TextExpanderViewModel.CaseSensitive) or nameof(TextExpanderViewModel.RequireWordBoundary))
                    && _isSnippetDialogOpen)
                {
                    Validate();
                }
                else if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision)
                         && _isSnippetDialogOpen
                         && _editingRevision != ViewModel.SnippetRevision)
                {
                    CancelPromptEdit();
                    Validate();
                }
            };
            QueueKeycapColumnUpdate();

            // The editor's discard prompt gets the same inset as Good to know.
            InsetLikeAnInfoBarMessage(DiscardWarningButtons);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Opens the Windows folder picker at <paramref name="startFolder"/> when it exists; null when cancelled.
        /// </summary>
        private async Task<string> PickSingleFolderDialog(string startFolder)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.EditorWindow);
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
                bool wasShown = HasSnippetProblem;
                if (Set(ref _snippetProblem, value))
                {
                    OnPropertyChanged(nameof(HasSnippetProblem));

                    // The bar announces itself as it opens, but not a different problem while it's open.
                    if (wasShown && HasSnippetProblem)
                    {
                        Announce(SnippetProblemBar, value, "textExpanderSnippetProblem");
                    }
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

        /// <summary>
        /// Insets custom InfoBar content the way the template insets its own message (InfoBarPanelMargin on the
        /// right, InfoBarPanelVerticalOrientationPadding below), which custom content doesn't get.
        /// </summary>
        private static void InsetLikeAnInfoBarMessage(FrameworkElement content)
            => content.Margin = new Thickness(0, 0, ThemeThickness("InfoBarPanelMargin").Right, ThemeThickness("InfoBarPanelVerticalOrientationPadding").Bottom);

        private static Thickness ThemeThickness(string key)
            => Application.Current.Resources.TryGetValue(key, out object value) && value is Thickness thickness ? thickness : default;

        /// <summary>
        /// Builds the Insert variable menu from the view model; its samples are refreshed each time it opens.
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
                if (variable.IsPrompt)
                {
                    item.Click += ConfigurePromptVariable_Click;
                    string help = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_Variable_Prompt_Help");
                    AutomationProperties.SetName(item, variable.Name);
                    AutomationProperties.SetHelpText(item, help);
                    ToolTipService.SetToolTip(item, help);
                }
                else
                {
                    item.Click += InsertVariable_Click;
                }

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
            if (_promptEditSession != null)
            {
                return;
            }

            (int start, int end) = _replacementEditor.Selection;
            BeginPromptEdit(TextExpanderPromptEditSession.BeginNew(_replacementEditor.Text, start, end));
        }

        private void ReplacementEditor_PromptEditRequested(object sender, TextExpanderPromptEditRequestedEventArgs e)
        {
            if (_promptEditSession == null
                && TextExpanderPromptEditSession.TryBeginExisting(_replacementEditor.Text, e.Start, e.End, out TextExpanderPromptEditSession session))
            {
                BeginPromptEdit(session);
            }
        }

        private void BeginPromptEdit(TextExpanderPromptEditSession session)
        {
            _promptEditSession = session;
            _promptFlyoutCloseIntent = PromptFlyoutCloseIntent.None;
            PromptQuestionBox.Text = session.Question;
            PromptRequireAnswer.IsChecked = session.RequireAnswer;
            PromptQuestionError.Text = string.Empty;
            PromptQuestionError.Visibility = Visibility.Collapsed;
            _replacementEditor.BeginTransientEdit(session.InitialText, session.TokenStart, session.TokenEnd);

            PromptFlyout.XamlRoot = XamlRoot;
            bool queued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (!ReferenceEquals(_promptEditSession, session))
                {
                    return;
                }

                try
                {
                    var options = new FlyoutShowOptions
                    {
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard,
                    };

                    if (_replacementEditor.PositionForRange(session.TokenStart, session.TokenEnd) is Point position)
                    {
                        options.Position = position;
                        PromptFlyout.ShowAt(ReplacementBox, options);
                    }
                    else
                    {
                        PromptFlyout.ShowAt(ReplacementBox);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or COMException or InvalidOperationException)
                {
                    CancelPromptEdit();
                }
            });
            if (!queued)
            {
                CancelPromptEdit();
            }
        }

        private void PromptFlyout_Opened(object sender, object e) => FocusPromptQuestion(selectAll: true);

        private void PromptQuestionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_promptEditSession == null)
            {
                return;
            }

            TextExpanderPromptValidationError error = TextExpanderPromptToken.Validate(PromptQuestionBox.Text);
            if (error == TextExpanderPromptValidationError.InvalidQuestion)
            {
                ShowPromptValidation(error, refocus: false);
            }
            else if (error == TextExpanderPromptValidationError.None)
            {
                ClearPromptValidation();
            }
            else if (PromptQuestionError.Visibility == Visibility.Visible)
            {
                ShowPromptValidation(error, refocus: false);
            }
        }

        private void PromptFlyoutContent_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                CancelPromptEdit();
            }
            else if (e.Key == global::Windows.System.VirtualKey.Enter && e.OriginalSource is not Button)
            {
                // A button gets its own Enter, so the close button closes without saving, as its name says.
                e.Handled = true;
                TryCommitPromptEdit(TextExpanderPromptEditCloseReason.Enter, hideFlyout: true);
            }
        }

        private void PromptFlyoutClose_Click(object sender, RoutedEventArgs e) => CancelPromptEdit();

        private void PromptFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            if (_promptFlyoutCloseIntent != PromptFlyoutCloseIntent.None || _promptEditSession == null)
            {
                return;
            }

            if (!TryCommitPromptEdit(TextExpanderPromptEditCloseReason.LightDismiss, hideFlyout: false))
            {
                args.Cancel = true;
            }
        }

        private void PromptFlyout_Closed(object sender, object e)
        {
            if (_promptEditSession != null)
            {
                CancelPromptEdit(hideFlyout: false);
            }

            _promptFlyoutCloseIntent = PromptFlyoutCloseIntent.None;
            FocusAfterLayout(ReplacementBox);
        }

        private bool TryCommitPromptEdit(TextExpanderPromptEditCloseReason reason, bool hideFlyout)
        {
            if (_promptEditSession == null)
            {
                return true;
            }

            TextExpanderPromptEditDecision decision = _promptEditSession.Resolve(
                reason,
                PromptQuestionBox.Text,
                PromptRequireAnswer.IsChecked != false);
            if (decision.Kind == TextExpanderPromptEditDecisionKind.KeepOpen)
            {
                ShowPromptValidation(decision.Error, refocus: true);
                return false;
            }

            _replacementEditor.CompleteTransientEdit(decision.Text, decision.SelectionStart, decision.SelectionEnd);
            _promptEditSession = null;
            _promptFlyoutCloseIntent = PromptFlyoutCloseIntent.Commit;
            ClearPromptValidation();
            if (hideFlyout && PromptFlyout.IsOpen)
            {
                PromptFlyout.Hide();
            }

            return true;
        }

        private void CancelPromptEdit(bool hideFlyout = true)
        {
            if (_promptEditSession == null)
            {
                return;
            }

            TextExpanderPromptEditDecision decision = _promptEditSession.Resolve(
                TextExpanderPromptEditCloseReason.Cancel,
                PromptQuestionBox.Text,
                PromptRequireAnswer.IsChecked != false);
            _replacementEditor.CancelTransientEdit(decision.Text, decision.SelectionStart, decision.SelectionEnd);
            _promptEditSession = null;
            _promptFlyoutCloseIntent = PromptFlyoutCloseIntent.Cancel;
            ClearPromptValidation();
            if (hideFlyout && PromptFlyout.IsOpen)
            {
                PromptFlyout.Hide();
            }
            else if (!PromptFlyout.IsOpen)
            {
                _promptFlyoutCloseIntent = PromptFlyoutCloseIntent.None;
            }
        }

        private void ShowPromptValidation(TextExpanderPromptValidationError error, bool refocus)
        {
            string key = error == TextExpanderPromptValidationError.InvalidQuestion
                ? "TextExpander_PromptFlyout_InvalidQuestion"
                : "TextExpander_PromptFlyout_QuestionRequired";
            string message = ResourceLoaderInstance.ResourceLoader.GetString(key);
            bool changed = PromptQuestionError.Visibility != Visibility.Visible
                || !string.Equals(PromptQuestionError.Text, message, StringComparison.Ordinal);
            PromptQuestionError.Text = message;
            PromptQuestionError.Visibility = Visibility.Visible;
            if (changed)
            {
                Announce(PromptQuestionError, "textExpanderPromptQuestionError");
            }

            if (refocus)
            {
                FocusPromptQuestion(selectAll: false);
            }
        }

        private void ClearPromptValidation()
        {
            PromptQuestionError.Text = string.Empty;
            PromptQuestionError.Visibility = Visibility.Collapsed;
        }

        private void FocusPromptQuestion(bool selectAll)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                PromptQuestionBox.Focus(FocusState.Programmatic);
                if (selectAll)
                {
                    PromptQuestionBox.SelectAll();
                }
            });
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
            bool hadError = ViewModel.HasSnippetError;
            ViewModel.LoadSnippets();
            if (!ViewModel.HasSnippetError)
            {
                SetSnippetStatus(
                    ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_Reloaded"),
                    "textExpanderSnippetFileReloaded");
            }
            else if (hadError)
            {
                // The error bar announces itself only as it opens, and a failed Retry leaves it open.
                Announce(SnippetErrorBar, ViewModel.SnippetError, "textExpanderSnippetError");
            }
        }

        private async void OpenSnippetsFile_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            string error = ViewModel.OpenSnippetsFile();
            if (error != null)
            {
                await ShowErrorAsync(error, "TextExpander_OpenSnippetsErrorDialog_Title");
            }
        }

        private async void EditSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            // Both the row and its menu's Edit come here.
            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(sender);
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

            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(sender);
            if (snippet == null)
            {
                return;
            }

            // The copy opens in the editor with a suggested trigger; nothing is written until Save.
            await ShowSnippetDialogAsync(null, ViewModel.SuggestDuplicateTrigger(snippet.Id) ?? string.Empty, snippet.Replacement);
        }

        private async void DeleteSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(sender);
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

            // Refused if the list reloaded while this asked, since the row may name another snippet now.
            string error = ViewModel.DeleteSnippet(snippet);
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

        /// <summary>
        /// Gets the sender's snippet, as <see cref="GetSnippetFromSender"/> does, or null after saying why its row
        /// can't be used: a row from before a reload may have a different snippet's ID now.
        /// </summary>
        private async Task<TextExpanderSnippetViewModel> GetListedSnippetAsync(object sender)
        {
            TextExpanderSnippetViewModel snippet = GetSnippetFromSender(sender);
            string problem = ViewModel.ValidateRow(snippet);
            if (problem == null)
            {
                return snippet;
            }

            await ShowErrorAsync(problem);
            return null;
        }

        /// <summary>Keeps the More options menu that is opening, which a reload then hides.</summary>
        private void RowMenu_Opening(object sender, object e) => _rowMenu = (MenuFlyout)sender;

        private async System.Threading.Tasks.Task ShowSnippetDialogAsync(int? id, string trigger, string replacement)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            _editingId = id;
            _editingRevision = ViewModel.SnippetRevision;
            _isSnippetDialogOpen = true;
            _originalSnippet = new TextExpanderSnippetViewModel(id ?? -1, trigger ?? string.Empty, replacement ?? string.Empty);
            _allowDialogClose = false;
            _closeWindowOnDiscard = false;
            ShowDiscardWarning = false;
            _lastEditedField = TriggerBox;
            CancelPromptEdit();
            SetSnippetStatus(string.Empty);
            TriggerBox.Text = trigger ?? string.Empty;
            _replacementEditor.SetText(replacement ?? string.Empty);

            SnippetDialog.XamlRoot = XamlRoot;
            Validate();

            ContentDialogResult result;
            try
            {
                result = await SnippetDialog.ShowAsync();
            }
            finally
            {
                _isSnippetDialogOpen = false;
                _editingId = null;
                CancelPromptEdit();
            }

            if (result == ContentDialogResult.Primary)
            {
                AnnounceSnippetChange("TextExpander_SnippetSaved");
            }
        }

        private void SnippetDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!TryCommitPromptEdit(TextExpanderPromptEditCloseReason.Enter, hideFlyout: true))
            {
                args.Cancel = true;
                return;
            }

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
            => args.Cancel = AskToDiscardChanges();

        /// <summary>
        /// Called as the user closes the window. With an edited snippet open, asks "Discard your changes?" as Cancel
        /// and Esc do, and returns true to keep the window open; Discard then closes it.
        /// </summary>
        internal bool AskToDiscardBeforeClosing()
        {
            _closeWindowOnDiscard = _isSnippetDialogOpen && AskToDiscardChanges();
            return _closeWindowOnDiscard;
        }

        /// <summary>Shows "Discard your changes?", with Keep editing focused, if the snippet has unsaved changes; returns whether it did.</summary>
        private bool AskToDiscardChanges()
        {
            // A close means discard the pending flyout edit, not light-dismiss-commit it.
            CancelPromptEdit();
            if (_allowDialogClose || !_originalSnippet.HasChanges(TriggerBox.Text, _replacementEditor.Text))
            {
                return false;
            }

            ShowDiscardWarning = true;
            FocusWhenShown(KeepEditingButton, DiscardWarning, () => Announce(DiscardWarning, DiscardWarning.Title, "textExpanderDiscardWarning"));
            return true;
        }

        private void KeepEditing_Click(object sender, RoutedEventArgs e)
        {
            ShowDiscardWarning = false;
            _closeWindowOnDiscard = false;

            // Back to whichever field the user was working in.
            (_lastEditedField ?? TriggerBox).Focus(FocusState.Programmatic);
        }

        private void SnippetField_GotFocus(object sender, RoutedEventArgs e) => _lastEditedField = (Control)sender;

        private void DiscardChanges_Click(object sender, RoutedEventArgs e)
        {
            _allowDialogClose = true;
            SnippetDialog.Hide();
            if (_closeWindowOnDiscard)
            {
                // The window was being closed and waited for this answer.
                App.EditorWindow?.Close();
            }
        }

        private void AnnounceSnippetChange(string resourceKey)
        {
            string message = ResourceLoaderInstance.ResourceLoader.GetString(resourceKey);
            if (ViewModel.HasSearchText)
            {
                message += " " + ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SearchStillActive");
            }

            SetSnippetStatus(message, "textExpanderSnippetChanged");
        }

        private void SetSnippetStatus(string text, string activityId = null)
        {
            SnippetStatus.Text = text ?? string.Empty;
            SnippetStatus.Visibility = string.IsNullOrEmpty(SnippetStatus.Text) ? Visibility.Collapsed : Visibility.Visible;
            if (!string.IsNullOrEmpty(activityId))
            {
                Announce(SnippetStatus, activityId);
            }
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
                if (!string.IsNullOrEmpty(backup))
                {
                    error += " " + string.Format(
                        CultureInfo.CurrentCulture,
                        ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_RecoveryAvailable"),
                        backup);
                }

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

        private void ReportStorageChange(string message, string backup)
        {
            if (!string.IsNullOrEmpty(backup))
            {
                message += " " + string.Format(
                    CultureInfo.CurrentCulture,
                    ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFolder_BackupCreated"),
                    backup);
            }

            SetSnippetStatus(message, "textExpanderSnippetStorageChanged");
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

            // Only an existing snippet's ID can name a different entry after a reload. A new one is checked
            // against the reloaded file below, so its Save stays available.
            if (_isSnippetDialogOpen && _editingId.HasValue && _editingRevision != ViewModel.SnippetRevision)
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

        private async System.Threading.Tasks.Task ShowErrorAsync(
            string message,
            string titleResourceKey = "TextExpander_SaveSnippetsErrorDialog_Title")
        {
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resourceLoader.GetString(titleResourceKey),
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
