// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.PowerToys.TextExpander.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.TextExpander.Editor.Views
{
    public sealed partial class TextExpanderPage : Page, INotifyPropertyChanged
    {
        // A preview line's height, a bubble's included.
        private const double PreviewLineHeight = 22;

        private const string SnippetsFileExtension = ".json";

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
        private bool _isTriggerInvalid;
        private double _dialogRoom = double.NaN;
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
            ViewModel = new TextExpanderViewModel(settingsUtils, isElevated);
            DataContext = ViewModel;
            this.InitializeComponent();

            // An InfoBar announces itself as it opens only if it already has an automation peer, which nothing
            // makes for a collapsed one, so these get theirs now.
            FrameworkElementAutomationPeer.CreatePeerForElement(SnippetErrorBar);
            FrameworkElementAutomationPeer.CreatePeerForElement(SnippetProblemBar);
            FrameworkElementAutomationPeer.CreatePeerForElement(SnippetStatus);

            // Narrator reads the privacy warning under the Replacement box along with the box's own help.
            AutomationProperties.SetHelpText(ReplacementBox, $"{AutomationProperties.GetHelpText(ReplacementBox)} {SensitiveInfoWarning.Text}".Trim());
            BuildVariableMenu();
            BuildDateFormatGuide();

            // The title bar and the prompt preview show the window's icon, which the engine project copies beside this app.
            AppTitleBar.Title = ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_EditorTitle");
            string icon = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(icon))
            {
                PromptPreviewIcon.Source = new BitmapImage(new Uri(icon));
                AppTitleBar.IconSource = new ImageIconSource { ImageSource = new BitmapImage(new Uri(icon)) };
            }

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
            Loaded += (_, _) => XamlRoot.Changed += (_, _) => FitSnippetDialog();
            Unloaded += (_, _) =>
            {
                _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                CancelPromptEdit();
            };
            SnippetDialog.Opened += (_, _) => _replacementEditor.Recolor();
            ShowSyncAdvice();
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TextExpanderViewModel.SyncSteps))
                {
                    ShowSyncAdvice();
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

            // The editor's discard prompt gets the inset an InfoBar gives its own message.
            InsetLikeAnInfoBarMessage(DiscardWarningButtons);
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        /// <summary>Gets a value indicating whether the trigger is what keeps the snippet from saving, which turns its box red.</summary>
        public bool IsTriggerInvalid
        {
            get => _isTriggerInvalid;
            private set => Set(ref _isTriggerInvalid, value);
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
            BeginPromptEdit(TextExpanderPromptEditSession.BeginNew(_replacementEditor.Text, start, end, PromptQuestionBox.PlaceholderText));
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
            PromptRequired.IsOn = session.RequireAnswer;
            PromptAddButton.Content = ResourceLoaderInstance.ResourceLoader.GetString(session.IsNew ? "TextExpander_PromptFlyout_Add" : "TextExpander_PromptFlyout_Update");
            ClearPromptValidation();
            _replacementEditor.BeginTransientEdit(session.InitialText, session.TokenStart, session.TokenEnd);
            UpdatePromptDraft();

            PromptFlyout.XamlRoot = XamlRoot;
            bool queued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (!ReferenceEquals(_promptEditSession, session))
                {
                    return;
                }

                try
                {
                    ShowPromptFlyout(session);
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

        /// <summary>Shows the question flyout as <see cref="TextExpanderPromptPlacement"/> places it against the token.</summary>
        private void ShowPromptFlyout(TextExpanderPromptEditSession session)
        {
            if (_replacementEditor.BoundsForRange(session.TokenStart, session.TokenEnd) is not Rect token)
            {
                PromptFlyout.ShowAt(ReplacementBox);
                return;
            }

            // Measured as it will show, with its preview and without, at the width it will have.
            Rect box = BoundsOf(ReplacementBox);
            (Rect save, Rect cancel) = DialogButtons();
            Thickness border = ThemeThickness("FlyoutBorderThemeThickness");
            PromptFlyoutContent.Width = Math.Max(0, TextExpanderPromptPlacement.Span(box, save, cancel, XamlRoot.Size).Width - border.Left - border.Right);
            double Height(bool preview)
            {
                PromptPreview.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
                PromptFlyoutContent.Measure(new Size(PromptFlyoutContent.Width, double.PositiveInfinity));
                return PromptFlyoutContent.DesiredSize.Height + border.Top + border.Bottom;
            }

            TextExpanderPromptPlacement placement = TextExpanderPromptPlacement.Place(token, box, XamlRoot.Size, Height(true), Height(false), save, cancel, BoundsOf(InsertVariableButton));

            // Capped, so the flyout never grows over the token, which WinUI would do to keep it in the window, and
            // grown when it would otherwise cut through a button.
            PromptPreview.Visibility = placement.ShowsPreview ? Visibility.Visible : Visibility.Collapsed;
            PromptFlyoutScroller.MinHeight = Math.Max(0, placement.MinHeight - border.Top - border.Bottom);
            PromptFlyoutScroller.MaxHeight = Math.Max(0, placement.MaxHeight - border.Top - border.Bottom);
            PromptFlyout.ShowAt(ReplacementBox, new FlyoutShowOptions
            {
                Placement = placement.Above ? FlyoutPlacementMode.TopEdgeAlignedLeft : FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Position = new Point(placement.X - box.X, placement.Y - box.Y),
                ShowMode = FlyoutShowMode.Standard,
            });
        }

        /// <summary>
        /// Gets the dialog's Save and Cancel from its command row, since an InfoBar in the content has a CloseButton too,
        /// or the content's bottom edge for both, which keeps the flyout in the content.
        /// </summary>
        private (Rect Save, Rect Cancel) DialogButtons()
        {
            FrameworkElement commands = SnippetDialogContent.FindAscendant("DialogSpace")?.FindDescendant("CommandSpace");
            if (commands?.FindDescendant("PrimaryButton") is FrameworkElement save && commands.FindDescendant("CloseButton") is FrameworkElement cancel)
            {
                return (BoundsOf(save), BoundsOf(cancel));
            }

            Rect content = BoundsOf(SnippetDialogContent);
            var bottom = new Rect(content.X, content.Bottom, content.Width, 0);
            return (bottom, bottom);
        }

        private static Rect BoundsOf(FrameworkElement element)
            => element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

        private void PromptFlyout_Opened(object sender, object e) => FocusPromptQuestion(selectAll: true);

        private void PromptQuestionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_promptEditSession == null)
            {
                return;
            }

            UpdatePromptDraft();

            // An error shows only once adding is refused (Add, Enter or a click outside); then it follows the typing.
            if (PromptQuestionError.Visibility == Visibility.Visible)
            {
                TextExpanderPromptValidationError error = TextExpanderPromptToken.Validate(PromptQuestionBox.Text);
                if (error == TextExpanderPromptValidationError.None)
                {
                    ClearPromptValidation();
                }
                else
                {
                    ShowPromptValidation(error, refocus: false);
                }
            }
        }

        private void PromptRequired_Toggled(object sender, RoutedEventArgs e) => UpdatePromptDraft();

        /// <summary>
        /// Shows the question as it's typed: in the token, as the snippet will read, and in the preview, as the
        /// window will ask it when the snippet runs.
        /// </summary>
        private void UpdatePromptDraft()
        {
            if (_promptEditSession == null)
            {
                return;
            }

            (string text, int tokenStart, int tokenEnd) = _promptEditSession.Draft(PromptQuestionBox.Text, PromptRequired.IsOn, PromptQuestionBox.PlaceholderText);
            _replacementEditor.UpdateTransientEdit(text, tokenStart, tokenEnd);
            PromptPreviewLabel.Text = PromptQuestionBox.Text.Trim();
            PromptPreviewPlaceholder.Visibility = PromptPreviewLabel.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

            // As in the window: OK waits for a required answer.
            PromptPreviewOk.IsEnabled = !PromptRequired.IsOn;
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
                // A button gets its own Enter, so Enter on Cancel cancels, as its name says.
                e.Handled = true;
                TryCommitPromptEdit(TextExpanderPromptEditCloseReason.Enter, hideFlyout: true);
            }
        }

        private void PromptAdd_Click(object sender, RoutedEventArgs e) => TryCommitPromptEdit(TextExpanderPromptEditCloseReason.Enter, hideFlyout: true);

        private void PromptCancel_Click(object sender, RoutedEventArgs e) => CancelPromptEdit();

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
                PromptRequired.IsOn);
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
                PromptRequired.IsOn);
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

        /// <summary>Ctrl+F goes to the search in the title bar, as in Keyboard Manager, unless the snippet dialog is open.</summary>
        private void SearchShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!_isSnippetDialogOpen)
            {
                SnippetSearchBox.Focus(FocusState.Keyboard);
                SnippetSearchBox.SelectAll();
                args.Handled = true;
            }
        }

        private void ReloadSnippets_Click(object sender, RoutedEventArgs e)
        {
            SnippetFolderFlyout.Hide();
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
            SnippetFolderFlyout.Hide();
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

        private async void SnippetList_ItemClick(object sender, ItemClickEventArgs e)
            => await EditSnippetAsync(e.ClickedItem as TextExpanderSnippetViewModel);

        private async void EditSnippet_Click(object sender, RoutedEventArgs e)
            => await EditSnippetAsync(GetSnippetFromSender(sender));

        private async Task EditSnippetAsync(TextExpanderSnippetViewModel row)
        {
            if (!ViewModel.CanEditSnippets)
            {
                return;
            }

            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(row);
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

            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(GetSnippetFromSender(sender));
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

            TextExpanderSnippetViewModel snippet = await GetListedSnippetAsync(GetSnippetFromSender(sender));
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
        /// Gets the snippet an item in a row's menu stands for. A menu isn't in the list's tree, so each item carries
        /// its row's snippet in Tag="{x:Bind}", which stays current as the list reuses its rows.
        /// </summary>
        private static TextExpanderSnippetViewModel GetSnippetFromSender(object sender)
            => (sender as FrameworkElement)?.Tag as TextExpanderSnippetViewModel;

        /// <summary>
        /// Gets <paramref name="snippet"/>, or null after saying why its row can't be used: a row from before a reload
        /// may have a different snippet's ID now.
        /// </summary>
        private async Task<TextExpanderSnippetViewModel> GetListedSnippetAsync(TextExpanderSnippetViewModel snippet)
        {
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
            SnippetDialog.Title = ResourceLoaderInstance.ResourceLoader.GetString(id.HasValue ? "TextExpander_SnippetDialog_EditTitle" : "TextExpander_SnippetDialog_NewTitle");
            FitSnippetDialog();
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

                // Lets go of the texts kept for undo.
                _replacementEditor.ClearHistory();
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
            bool wasOpen = SnippetStatus.IsOpen;
            SnippetStatus.Message = text ?? string.Empty;
            SnippetStatus.IsOpen = SnippetStatus.Message.Length > 0;
            SnippetStatus.Visibility = SnippetStatus.IsOpen ? Visibility.Visible : Visibility.Collapsed;

            // The bar announces itself as it opens, but not a new message while it's open.
            if (wasOpen && SnippetStatus.IsOpen && !string.IsNullOrEmpty(activityId))
            {
                Announce(SnippetStatus, SnippetStatus.Message, activityId);
            }
        }

        private void SnippetStatus_Closed(InfoBar sender, InfoBarClosedEventArgs args) => sender.Visibility = Visibility.Collapsed;

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

        /// <summary>
        /// Moves the snippets to a file picked in the Save dialog: Windows asks before replacing a file, and one that has
        /// snippets of its own then asks whether to use them instead.
        /// </summary>
        private async void MoveSnippets_Click(object sender, RoutedEventArgs e)
        {
            SnippetFolderFlyout.Hide();
            var picker = new FileSavePicker(EditorWindowId())
            {
                SuggestedFileName = Path.GetFileName(ViewModel.SnippetsFilePath),
                DefaultFileExtension = SnippetsFileExtension,
            };
            picker.FileTypeChoices.Add(ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFile_FileType"), new List<string> { SnippetsFileExtension });
            if (ViewModel.SnippetsPickerFolder is string folder && Directory.Exists(folder))
            {
                picker.SuggestedFolder = folder;
            }
            else
            {
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            }

            string path = (await picker.PickSaveFileAsync())?.Path;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TextExpanderFileConflictChoice choice = TextExpanderFileConflictChoice.ReplaceFile;
            if (ViewModel.FileHasSnippets(path))
            {
                TextExpanderFileConflictChoice? selected = await ConfirmFileHasSnippetsAsync(path);
                if (!selected.HasValue)
                {
                    return;
                }

                choice = selected.Value;
            }

            string error = ViewModel.MoveSnippetsTo(path, choice, out string status);
            if (error != null)
            {
                await ShowStorageErrorAsync(error);
                return;
            }

            // Moving onto the database already in use changes nothing, so the status bar stays as it was.
            if (status != null)
            {
                SetSnippetStatus(status, "textExpanderSnippetStorageChanged");
            }
        }

        /// <summary>
        /// Uses a snippets file picked in the Open dialog, from the flyout or the error bar. A file that isn't one says so,
        /// and Choose a different file opens the dialog again.
        /// </summary>
        private async void UseSnippetsFile_Click(object sender, RoutedEventArgs e)
        {
            SnippetFolderFlyout.Hide();
            while (true)
            {
                var picker = new FileOpenPicker(EditorWindowId());
                picker.FileTypeChoices.Add(ResourceLoaderInstance.ResourceLoader.GetString("TextExpander_SnippetFile_FileType"), new List<string> { SnippetsFileExtension });
                if (ViewModel.SnippetsPickerFolder is string folder && Directory.Exists(folder))
                {
                    picker.SuggestedFolder = folder;
                }
                else
                {
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                }

                string path = (await picker.PickSingleFileAsync())?.Path;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                string error = ViewModel.UseSnippetsFile(path, out string status);
                if (error == null)
                {
                    SetSnippetStatus(status, "textExpanderSnippetStorageChanged");
                    return;
                }

                var resources = ResourceLoaderInstance.ResourceLoader;
                if (error != string.Format(CultureInfo.CurrentCulture, resources.GetString("TextExpander_SnippetFile_NotSnippets"), Path.GetFileName(path)))
                {
                    await ShowStorageErrorAsync(error);
                    return;
                }

                var refused = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = error,
                    PrimaryButtonText = resources.GetString("TextExpander_SnippetFile_ChooseDifferent"),
                    CloseButtonText = resources.GetString("TextExpander_Dialog_Close"),
                    DefaultButton = ContentDialogButton.Primary,
                };
                if (await refused.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }
        }

        /// <summary>Asks whether to use the snippets already in the file at <paramref name="path"/> or replace them; null for Cancel, the default.</summary>
        private async Task<TextExpanderFileConflictChoice?> ConfirmFileHasSnippetsAsync(string path)
        {
            var resources = ResourceLoaderInstance.ResourceLoader;
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = resources.GetString("TextExpander_SnippetFile_Conflict_Title"),
                Content = new TextBlock
                {
                    Text = string.Format(CultureInfo.CurrentCulture, resources.GetString("TextExpander_SnippetFile_Conflict_Message"), Path.GetFileName(path)),
                    TextWrapping = TextWrapping.Wrap,
                },
                PrimaryButtonText = resources.GetString("TextExpander_SnippetFile_Conflict_Use"),
                SecondaryButtonText = resources.GetString("TextExpander_SnippetFile_Conflict_Replace"),
                CloseButtonText = resources.GetString("TextExpander_Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };

            return await confirm.ShowAsync() switch
            {
                ContentDialogResult.Primary => TextExpanderFileConflictChoice.UseFile,
                ContentDialogResult.Secondary => TextExpanderFileConflictChoice.ReplaceFile,
                _ => null,
            };
        }

        private static Microsoft.UI.WindowId EditorWindowId()
            => Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(App.EditorWindow));

        /// <summary>
        /// Shows the sync advice's steps under its heading: a numbered list, which a screen reader reads as "1 of 2", or
        /// one sentence. Each step's button name is bold.
        /// </summary>
        private void ShowSyncAdvice()
        {
            IReadOnlyList<TextExpanderSyncStep> steps = ViewModel.SyncSteps;
            SyncStepList.Children.Clear();
            for (int i = 0; i < steps.Count; i++)
            {
                var sentence = new Paragraph();
                sentence.Inlines.Add(new Run { Text = steps[i].Before });
                sentence.Inlines.Add(new Run { Text = steps[i].Button, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                sentence.Inlines.Add(new Run { Text = steps[i].After });
                var text = new RichTextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = false };
                text.Blocks.Add(sentence);
                if (steps.Count == 1)
                {
                    SyncStepList.Children.Add(text);
                    continue;
                }

                AutomationProperties.SetAutomationControlType(text, AutomationControlType.ListItem);
                AutomationProperties.SetPositionInSet(text, i + 1);
                AutomationProperties.SetSizeOfSet(text, steps.Count);
                var number = new TextBlock { Text = (i + 1).ToString(CultureInfo.CurrentCulture) + ".", Width = 20 };
                AutomationProperties.SetAccessibilityView(number, AccessibilityView.Raw);
                Grid.SetColumn(text, 1);
                var item = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() } };
                item.Children.Add(number);
                item.Children.Add(text);
                SyncStepList.Children.Add(item);
            }
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
                IsTriggerInvalid = false;
                return;
            }

            // Quiet while the dialog is still empty, but once there's text a missing trigger
            // says why Save is off.
            string problem = ViewModel.ValidateSnippet(_editingId, trigger);
            SnippetProblem = trigger.Length == 0 && replacement.Length == 0 ? null : problem;
            IsSnippetValid = problem == null;

            // The trigger is what's wrong when it breaks a rule, or when it's taken; any other problem is the file's.
            IsTriggerInvalid = HasSnippetProblem && (!SnippetFile.IsValidTrigger(trigger, out _) || !ViewModel.HasSnippetError);
        }

        /// <summary>
        /// Shows eight preview lines at most, and once the dialog is too short for its content, only the whole lines that
        /// fit, one at least, so its edge never cuts a line in half. The rest of the content doesn't depend on the preview.
        /// </summary>
        private void SnippetDialogLayout_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // A dialog that scrolls is as tall as the window lets it be, so its viewport is the room there is.
            if (SnippetDialogScroller.ScrollableHeight > 0)
            {
                _dialogRoom = SnippetDialogScroller.ViewportHeight;
            }

            double rest = SnippetDialogContent.ActualHeight - SnippetPreviewText.ActualHeight;
            SnippetPreviewText.MaxLines = double.IsNaN(_dialogRoom) ? 8 : Math.Clamp((int)((_dialogRoom - rest) / PreviewLineHeight), 1, 8);
        }

        /// <summary>Sizes the dialog's content as Keyboard Manager's, 760 wide, or as wide as the window leaves room for.</summary>
        private void FitSnippetDialog()
        {
            if (XamlRoot != null)
            {
                SnippetDialogContent.Width = Math.Clamp(XamlRoot.Size.Width - 96, 320, 760);
            }

            // A new window size or a new dialog: the preview tries eight lines again.
            _dialogRoom = double.NaN;
            SnippetPreviewText.MaxLines = 8;
        }

        /// <summary>
        /// Shows what the snippet types, with a bubble for each value that is only known when it
        /// runs: the clipboard, an answer, or a date or time (shown as it is now).
        /// </summary>
        private void ShowPreview(string replacement)
        {
            IReadOnlyList<TextExpanderPreviewSegment> segments = TextExpanderSnippetPreview.Build(replacement, DateTime.Now);
            SnippetPreviewText.Segments = segments;
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
