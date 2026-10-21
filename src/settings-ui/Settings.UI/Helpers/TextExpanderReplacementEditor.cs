// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Makes a <see cref="RichEditBox"/> a plain-text box for a snippet's replacement, with its
    /// variables colored and each one handled as a unit.
    ///
    /// <para>
    /// The box only ever holds plain text: paste drops formatting, formatting shortcuts are off,
    /// and the colors are reapplied after every change. The text it reports uses \n line breaks,
    /// like the plain text box it replaces, so saving writes the same bytes. Undo comes from
    /// <see cref="TextExpanderEditHistory"/> rather than RichEdit, which would also undo colors.
    /// </para>
    /// </summary>
    internal sealed class TextExpanderReplacementEditor
    {
        private readonly RichEditBox _box;
        private readonly Func<(Color Text, Color Background)?> _variableColors;
        private readonly TextExpanderEditHistory _history = new();
        private string _text = string.Empty;
        private bool _updating;
        private bool _composing;
        private bool _recolorQueued;
        private (int Start, int End)? _variableSelectedByClick;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextExpanderReplacementEditor"/> class.
        /// </summary>
        /// <param name="box">The box to manage.</param>
        /// <param name="variableColors">Supplies the colors for variables, or null to leave them uncolored, as in high contrast.</param>
        public TextExpanderReplacementEditor(RichEditBox box, Func<(Color Text, Color Background)?> variableColors)
        {
            _box = box;
            _variableColors = variableColors;

            box.Document.UndoLimit = 0;
            box.SelectionFlyout = null;
            box.ContextFlyout = CreateContextMenu();

            box.TextChanging += (_, args) =>
            {
                if (args.IsContentChanging)
                {
                    OnContentChanged();
                }
            };
            box.TextChanged += (_, _) => OnContentChanged();
            box.TextCompositionStarted += (_, _) => _composing = true;
            box.TextCompositionEnded += (_, _) => EndComposition();
            box.LostFocus += (_, _) =>
            {
                EndComposition();
                _history.EndRun();
                QueueRecolor();
            };
            box.Paste += Box_Paste;
            box.PreviewKeyDown += Box_PreviewKeyDown;
            box.SelectionChanged += Box_SelectionChanged;
            box.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Box_PointerReleased), handledEventsToo: true);

            // The text color is set explicitly (see Recolor), so it is taken again from the box
            // whenever the box's own text color can change: once it is shown and themed, on a theme
            // change, and when its focus or enabled state changes.
            box.Loaded += (_, _) => QueueRecolor();
            box.ActualThemeChanged += (_, _) => QueueRecolor();
            box.GotFocus += (_, _) => QueueRecolor();
            box.IsEnabledChanged += (_, _) => QueueRecolor();
        }

        /// <summary>Raised after the user changes the text.</summary>
        public event EventHandler TextChanged;

        /// <summary>Raised when the caret or selection moves, including when the editor moves it.</summary>
        public event EventHandler SelectionChanged;

        /// <summary>Gets the replacement as typed, with \n line breaks.</summary>
        public string Text => ReadText();

        /// <summary>Gets the selection, as positions in <see cref="Text"/>.</summary>
        public (int Start, int End) Selection => GetSelection();

        /// <summary>
        /// Colors the text: everything in the box's own text color, then each variable in accent
        /// colors, or underlined in high contrast. Call after the theme or high contrast changes.
        ///
        /// <para>
        /// Ordinary text is colored explicitly rather than left "automatic": in a RichEditBox the
        /// automatic color is the Win32 window text color, black even in dark mode, and typed text
        /// takes the color of the character before it, so text typed after a variable would
        /// otherwise come out in the variable's accent color.
        /// </para>
        /// </summary>
        public void Recolor()
        {
            if (_composing)
            {
                return;
            }

            (Color Text, Color Background)? colors = _variableColors();
            RichEditTextDocument document = _box.Document;
            Color plain = TextExpanderEditorColors.PlainText(
                document.GetDefaultCharacterFormat().ForegroundColor,
                TextConstants.AutoColor,
                TextConstants.UndefinedColor,
                (_box.Foreground as SolidColorBrush)?.Color ?? TextConstants.AutoColor);

            Update(() =>
            {
                document.BatchDisplayUpdates();
                try
                {
                    // The whole story, including its final paragraph mark, so text typed at the
                    // end of the box or into an empty box starts out in the right color.
                    ITextCharacterFormat all = document.GetRange(0, TextConstants.MaxUnitCount).CharacterFormat;
                    all.ForegroundColor = plain;

                    // RichEdit paints no background for the automatic background color.
                    all.BackgroundColor = TextConstants.AutoColor;
                    all.Underline = UnderlineType.None;

                    bool underline = TextExpanderEditorColors.UnderlinesVariables(colors);
                    foreach ((int start, int end) in TextExpanderTokenText.FindVariables(_text))
                    {
                        ITextCharacterFormat format = document.GetRange(start, end).CharacterFormat;
                        if (colors is { } tint)
                        {
                            format.ForegroundColor = tint.Text;
                            format.BackgroundColor = tint.Background;
                        }

                        if (underline)
                        {
                            format.Underline = UnderlineType.Single;
                        }
                    }
                }
                finally
                {
                    document.ApplyDisplayUpdates();
                }
            });
        }

        /// <summary>Shows <paramref name="text"/> for editing, with nothing to undo.</summary>
        public void SetText(string text)
        {
            _composing = false;
            _variableSelectedByClick = null;
            Update(() => _box.Document.SetText(TextSetOptions.None, TextExpanderTokenText.ToEditor(text)));
            _text = ReadText();
            _history.Reset(_text);
            Recolor();
            Select(0, 0);
        }

        /// <summary>
        /// Inserts a variable at the caret, replacing any selection, but never inside another
        /// variable. Selects a question's label or a date's format so it can be typed over, unless
        /// <paramref name="selectEditablePart"/> is false, as for a ready-made date format.
        /// </summary>
        public void InsertVariable(string variable, bool selectEditablePart = true)
        {
            OnContentChanged();
            (int selectionStart, int selectionEnd) = GetSelection();
            InsertVariableAt(variable, selectionStart, selectionEnd, selectEditablePart);
        }

        /// <summary>
        /// Inserts a variable at a selection captured before another control took focus.
        /// </summary>
        public void InsertVariableAt(string variable, int selectionStart, int selectionEnd, bool selectEditablePart = true)
        {
            OnContentChanged();
            (int start, int end, int selectStart, int selectEnd) = TextExpanderTokenText.PlanInsert(_text, selectionStart, selectionEnd, variable, selectEditablePart);
            Replace(start, end, variable, selectStart, selectEnd);
        }

        /// <summary>
        /// Puts a code from the date format guide into the {{date:…}} variable the caret is in (see
        /// <see cref="TextExpanderDateGuide.CodeInsertion"/>). Returns false when it isn't in one.
        /// </summary>
        public bool InsertDateCode(string code)
        {
            OnContentChanged();
            (int selectionStart, int selectionEnd) = GetSelection();
            if (TextExpanderDateGuide.CodeInsertion(_text, selectionStart, selectionEnd, code) is not { } insertion)
            {
                return false;
            }

            Replace(insertion.Start, insertion.End, code, insertion.Caret, insertion.Caret);
            return true;
        }

        public void Undo()
        {
            OnContentChanged();
            if (_history.TryUndo(out string text, out int selectionStart, out int selectionEnd))
            {
                Restore(text, selectionStart, selectionEnd);
            }
        }

        public void Redo()
        {
            OnContentChanged();
            if (_history.TryRedo(out string text, out int selectionStart, out int selectionEnd))
            {
                Restore(text, selectionStart, selectionEnd);
            }
        }

        private static bool IsDown(VirtualKey key)
            => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

        /// <summary>Recolors once the box has settled, for example after its theme or state changed.</summary>
        private void QueueRecolor()
        {
            if (_recolorQueued)
            {
                return;
            }

            _recolorQueued = true;
            _box.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _recolorQueued = false;
                Recolor();
            });
        }

        /// <summary>
        /// Gets a value indicating whether RichEdit may handle <paramref name="key"/> with Ctrl.
        /// The rest are its paragraph and font shortcuts (Ctrl+E centers, Ctrl+1 sets line spacing,
        /// Ctrl+Shift+L makes a list), which would format text the snippet cannot keep.
        /// </summary>
        private static bool IsAllowedWithControl(VirtualKey key) => key switch
        {
            VirtualKey.A or VirtualKey.C or VirtualKey.V or VirtualKey.X => true,
            VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down => true,
            VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown => true,
            VirtualKey.Back or VirtualKey.Delete or VirtualKey.Insert or VirtualKey.Space or VirtualKey.Escape => true,
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => true,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => true,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => true,
            VirtualKey.LeftWindows or VirtualKey.RightWindows => true,
            >= VirtualKey.F1 and <= VirtualKey.F24 => true,
            _ => false,
        };

        private static bool CanPaste()
        {
            try
            {
                return Clipboard.GetContent().Contains(StandardDataFormats.Text);
            }
            catch (COMException)
            {
                // Another app has the clipboard open.
                return false;
            }
        }

        private static MenuFlyoutItem CreateMenuItem(StandardUICommandKind kind, Action action)
        {
            // Borrows the system's localized name, icon and shortcut text. The shortcut itself stays
            // off: the box already handles the key, and must not act on it twice.
            var command = new StandardUICommand(kind);
            var item = new MenuFlyoutItem { Text = command.Label };
            if (command.IconSource != null)
            {
                item.Icon = new IconSourceElement { IconSource = command.IconSource };
            }

            if (command.KeyboardAccelerators.Count > 0)
            {
                KeyboardAccelerator shortcut = command.KeyboardAccelerators[0];
                item.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = shortcut.Key, Modifiers = shortcut.Modifiers, IsEnabled = false });
            }

            item.Click += (_, _) => action();
            return item;
        }

        private MenuFlyout CreateContextMenu()
        {
            MenuFlyoutItem cut = CreateMenuItem(StandardUICommandKind.Cut, Cut);
            MenuFlyoutItem copy = CreateMenuItem(StandardUICommandKind.Copy, Copy);
            MenuFlyoutItem paste = CreateMenuItem(StandardUICommandKind.Paste, () => _ = PastePlainTextAsync());
            MenuFlyoutItem undo = CreateMenuItem(StandardUICommandKind.Undo, Undo);
            MenuFlyoutItem redo = CreateMenuItem(StandardUICommandKind.Redo, Redo);
            MenuFlyoutItem selectAll = CreateMenuItem(StandardUICommandKind.SelectAll, () => Select(0, _text.Length));

            var menu = new MenuFlyout();
            menu.Items.Add(cut);
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(undo);
            menu.Items.Add(redo);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(selectAll);

            menu.Opening += (_, _) =>
            {
                OnContentChanged();
                (int start, int end) = GetSelection();
                cut.IsEnabled = start != end;
                copy.IsEnabled = start != end;
                paste.IsEnabled = CanPaste();
                undo.IsEnabled = _history.CanUndo;
                redo.IsEnabled = _history.CanRedo;
                selectAll.IsEnabled = _text.Length > 0;
            };

            return menu;
        }

        private void OnContentChanged()
        {
            if (!_updating && !_composing)
            {
                Commit();
            }
        }

        /// <summary>Takes in a change the user made: records it for undo and recolors.</summary>
        private void Commit()
        {
            string text = ReadText();
            if (string.Equals(text, _text, StringComparison.Ordinal))
            {
                // Only formatting changed, such as the recoloring itself.
                return;
            }

            _text = text;
            _history.Record(text);
            Recolor();
            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EndComposition()
        {
            if (_composing)
            {
                _composing = false;
                OnContentChanged();
            }
        }

        private string ReadText()
        {
            RichEditTextDocument document = _box.Document;
            document.GetText(TextGetOptions.AllowFinalEop, out string raw);
            return TextExpanderTokenText.FromEditor(raw, document.Selection.StoryLength);
        }

        /// <summary>Changes the box without treating the change as typing.</summary>
        private void Update(Action change)
        {
            bool wasUpdating = _updating;
            _updating = true;
            try
            {
                change();
            }
            finally
            {
                _updating = wasUpdating;
            }
        }

        /// <summary>Replaces part of the text as one undo step, then selects the given range.</summary>
        private void Replace(int start, int end, string text, int selectionStart, int selectionEnd)
        {
            _history.EndRun();
            Update(() => _box.Document.GetRange(start, end).SetText(TextSetOptions.None, TextExpanderTokenText.ToEditor(text)));
            Commit();
            _history.EndRun();
            Select(selectionStart, selectionEnd);
        }

        /// <summary>Puts back a text from the history, changing only the part that differs.</summary>
        private void Restore(string text, int selectionStart, int selectionEnd)
        {
            (int start, int removed, string inserted) = TextExpanderTokenText.Diff(_text, text);
            Update(() => _box.Document.GetRange(start, start + removed).SetText(TextSetOptions.None, TextExpanderTokenText.ToEditor(inserted)));
            _text = ReadText();
            if (!string.Equals(_text, text, StringComparison.Ordinal))
            {
                // Never leave the box and the history disagreeing.
                Update(() => _box.Document.SetText(TextSetOptions.None, TextExpanderTokenText.ToEditor(text)));
                _text = ReadText();
            }

            Recolor();
            Select(selectionStart, selectionEnd);
            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        private (int Start, int End) GetSelection()
        {
            ITextSelection selection = _box.Document.Selection;
            int start = Math.Min(selection.StartPosition, selection.EndPosition);
            int end = Math.Max(selection.StartPosition, selection.EndPosition);
            return (Math.Clamp(start, 0, _text.Length), Math.Clamp(end, 0, _text.Length));
        }

        private void Select(int start, int end)
        {
            int length = _text.Length;
            Update(() =>
            {
                ITextSelection selection = _box.Document.Selection;
                selection.SetRange(Math.Clamp(start, 0, length), Math.Clamp(end, 0, length));
                selection.ScrollIntoView(PointOptions.None);
            });
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Box_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_composing)
            {
                return;
            }

            bool control = IsDown(VirtualKey.Control);
            bool shift = IsDown(VirtualKey.Shift);
            bool alt = IsDown(VirtualKey.Menu);

            switch (e.Key)
            {
                case VirtualKey.Z when control && !alt:
                    if (shift)
                    {
                        Redo();
                    }
                    else
                    {
                        Undo();
                    }

                    e.Handled = true;
                    return;

                case VirtualKey.Y when control && !alt && !shift:
                    Redo();
                    e.Handled = true;
                    return;

                case VirtualKey.Back when alt && !control && !shift:
                    // The classic Windows undo key, which RichEdit would otherwise handle itself.
                    Undo();
                    e.Handled = true;
                    return;

                case VirtualKey.Back when !alt:
                    e.Handled = DeleteVariable(backward: true);
                    return;

                case VirtualKey.Delete when !alt && !shift:
                    e.Handled = DeleteVariable(backward: false);
                    return;

                case VirtualKey.Enter when shift && !control && !alt:
                    // RichEdit would insert a soft line break, a character the engine would type
                    // as it is. Shift+Enter has always meant a new line here.
                    _box.Document.Selection.TypeText("\r");
                    OnContentChanged();
                    e.Handled = true;
                    return;

                case VirtualKey.Tab when !control && !alt:
                    // RichEdit would insert a tab; the plain text box moved focus, which keyboard
                    // users need to reach Save. A tab can still be written as \t.
                    e.Handled = MoveFocus(backward: shift);
                    return;
            }

            if (control && !alt && !IsAllowedWithControl(e.Key))
            {
                e.Handled = true;
            }
        }

        private bool DeleteVariable(bool backward)
        {
            OnContentChanged();
            (int start, int end) = GetSelection();
            if (start != end || TextExpanderTokenText.VariableToDelete(_text, start, backward) is not { } variable)
            {
                return false;
            }

            Replace(variable.Start, variable.End, string.Empty, variable.Start, variable.Start);
            return true;
        }

        private bool MoveFocus(bool backward)
        {
            // Keep the search inside the dialog the box is in.
            DependencyObject root = _box;
            for (DependencyObject parent = VisualTreeHelper.GetParent(_box); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                root = parent;
                if (parent is ContentDialog)
                {
                    break;
                }
            }

            try
            {
                var options = new FindNextElementOptions { SearchRoot = root };
                return FocusManager.TryMoveFocus(backward ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next, options);
            }
            catch (Exception ex) when (ex is ArgumentException or COMException or InvalidOperationException)
            {
                // Let RichEdit have the key rather than lose it.
                return false;
            }
        }

        private void Box_SelectionChanged(object sender, RoutedEventArgs e)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            if (_updating || _variableSelectedByClick is not { } selected)
            {
                return;
            }

            // Forget the click once the selection moves anywhere but that variable or a caret in it.
            (int start, int end) = GetSelection();
            bool onVariable = (start == selected.Start && end == selected.End)
                || (start == end && selected.Start < start && start < selected.End);
            if (!onVariable)
            {
                _variableSelectedByClick = null;
            }
        }

        private void Box_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_composing || _updating)
            {
                return;
            }

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
                && e.GetCurrentPoint(_box).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
            {
                return;
            }

            OnContentChanged();
            (int start, int end) = GetSelection();
            (int Start, int End)? variable = start == end ? TextExpanderTokenText.VariableAround(_text, start) : null;
            if (variable == null || variable == _variableSelectedByClick)
            {
                // A second click in a variable the first click selected leaves the caret there,
                // so a question's label or a date's format can still be edited.
                _variableSelectedByClick = null;
                return;
            }

            _variableSelectedByClick = variable;
            Select(variable.Value.Start, variable.Value.End);
        }

        private async void Box_Paste(object sender, TextControlPasteEventArgs e)
        {
            // RichEdit would bring the copied formatting, links and pictures along.
            e.Handled = true;
            await PastePlainTextAsync();
        }

        private async Task PastePlainTextAsync()
        {
            string text;
            try
            {
                DataPackageView content = Clipboard.GetContent();
                if (!content.Contains(StandardDataFormats.Text))
                {
                    return;
                }

                text = await content.GetTextAsync();
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Another app has the clipboard open, or its text went away.
                return;
            }

            OnContentChanged();
            string pasted = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            (int start, int end) = GetSelection();
            Replace(start, end, pasted, start + pasted.Length, start + pasted.Length);
        }

        private void Copy() => TryCopy();

        private bool TryCopy()
        {
            OnContentChanged();
            (int start, int end) = GetSelection();
            if (start == end)
            {
                return false;
            }

            var package = new DataPackage();
            package.SetText(_text[start..end].Replace("\n", "\r\n", StringComparison.Ordinal));
            try
            {
                Clipboard.SetContent(package);
                return true;
            }
            catch (COMException)
            {
                // Another app has the clipboard open; nothing was copied.
                return false;
            }
        }

        private void Cut()
        {
            // Only remove what actually reached the clipboard.
            if (TryCopy())
            {
                (int start, int end) = GetSelection();
                Replace(start, end, string.Empty, start, start);
            }
        }
    }
}
