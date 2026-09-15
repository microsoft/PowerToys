// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Translation;
using ScreenTranslator.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Graphics;
using WinUIEx;

namespace ScreenTranslator;

public sealed partial class ResultOverlay : TransparentWindow
{
    private readonly ScreenInfo _screenInfo;
    private readonly PhysicalRect _overlayBounds;
    private readonly PhysicalRect _capturedRegion;
    private readonly IReadOnlyList<TranslatedLine> _lines;
    private readonly string _sourceLanguage;
    private readonly string _targetLanguage;
    private readonly Func<string, string, Task>? _retranslateAll;
    private readonly Func<TranslationLine, string, string, Task<TranslationResult>>? _retranslateLine;
    private readonly IntPtr _hwnd;
    private readonly List<(PhysicalRect Bounds, Border Card)> _cardHitRegions = new();
    private readonly HashSet<int> _hiddenLineIndices = new();
    private readonly Dictionary<int, (
        string Text,
        Windows.UI.Color Background,
        Windows.UI.Color Foreground,
        double FontSize,
        double Left,
        double Top,
        double Width,
        double MinHeight)> _initialAppearance = new();

    private readonly Dictionary<int, string> _translatedTexts = new();
    private readonly HashSet<int> _showingOriginalText = new();

    private readonly DispatcherQueueTimer _windowSwitchTimer;
    private readonly IntPtr _sourceWindow;
    private readonly long _shownTimestamp = Environment.TickCount64;
    private Border? _dragCard;
    private uint _dragPointerId;
    private Windows.Foundation.Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private int _contextMenuLineIndex = -1;
    private TranslatedLine? _contextMenuLine;
    private Border? _contextMenuCard;
    private bool _isInitializingColorPickers;
    private TextBox? _editingTextBox;
    private Border? _editingCard;
    private string _editingOriginalText = string.Empty;

    public ResultOverlay(
        ScreenInfo screenInfo,
        PhysicalRect capturedRegion,
        IReadOnlyList<TranslatedLine> lines,
        IntPtr sourceWindow,
        string sourceLanguage,
        string targetLanguage,
        Func<string, string, Task>? retranslateAll = null,
        Func<TranslationLine, string, string, Task<TranslationResult>>? retranslateLine = null)
    {
        _screenInfo = screenInfo;
        _overlayBounds = screenInfo.WorkingArea;
        _capturedRegion = capturedRegion;
        _lines = lines;
        _sourceLanguage = sourceLanguage;
        _targetLanguage = targetLanguage;
        _sourceWindow = sourceWindow;
        _retranslateAll = retranslateAll;
        _retranslateLine = retranslateLine;
        DismissOnFocusLost = false;

        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        int style = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlStyle, style & ~OSInterop.WsCaption & ~OSInterop.WsThickFrame);

        int exStyle = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlExStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlExStyle, exStyle | OSInterop.WsExNoActivate | OSInterop.WsExToolWindow | OSInterop.WsExTopMost);

        AppWindow.MoveAndResize(new RectInt32(
            (int)_overlayBounds.X,
            (int)_overlayBounds.Y,
            (int)_overlayBounds.Width,
            (int)_overlayBounds.Height));
        _ = OSInterop.SetWindowPos(
            _hwnd,
            OSInterop.HwndTopMost,
            (int)_overlayBounds.X,
            (int)_overlayBounds.Y,
            (int)_overlayBounds.Width,
            (int)_overlayBounds.Height,
            OSInterop.SwpNoActivate | OSInterop.SwpNoOwnerZOrder | OSInterop.SwpShowWindow);

        try
        {
            this.SetIsShownInSwitchers(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"SetIsShownInSwitchers failed: {ex.Message}");
        }

        Closed += ResultOverlay_Closed;
        _windowSwitchTimer = DispatcherQueue.CreateTimer();
        _windowSwitchTimer.Interval = TimeSpan.FromMilliseconds(400);
        _windowSwitchTimer.Tick += WindowSwitchTimer_Tick;
        _windowSwitchTimer.Start();

        RenderTranslatedBoxes();
        PositionToolbar();
        SelectLanguage(OverallSourceLanguageComboBox, sourceLanguage);
        SelectLanguage(OverallTargetLanguageComboBox, targetLanguage);
    }

    private void ResultOverlay_Closed(object sender, WindowEventArgs args)
    {
        _windowSwitchTimer.Stop();
    }

    private void WindowSwitchTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_sourceWindow == IntPtr.Zero)
        {
            return;
        }

        IntPtr foregroundWindow = OSInterop.GetForegroundWindow();
        if (Environment.TickCount64 - _shownTimestamp < 1000)
        {
            return;
        }

        if (foregroundWindow != IntPtr.Zero &&
            foregroundWindow != _sourceWindow &&
            !IsOwnedByOverlay(foregroundWindow) &&
            !BelongsToThisProcess(foregroundWindow) &&
            !IsShellWindow(foregroundWindow))
        {
            Logger.LogInfo($"Closing result overlay after source-window switch. Source=0x{_sourceWindow.ToInt64():X}, foreground=0x{foregroundWindow.ToInt64():X}.");
            Close();
        }
    }

    private static bool BelongsToThisProcess(IntPtr windowHandle)
    {
        return OSInterop.GetWindowThreadProcessId(windowHandle, out uint processId) != 0 &&
               processId == Environment.ProcessId;
    }

    private static bool IsShellWindow(IntPtr windowHandle)
    {
        if (OSInterop.GetWindowThreadProcessId(windowHandle, out uint processId) == 0)
        {
            return false;
        }

        try
        {
            return string.Equals(
                Process.GetProcessById((int)processId).ProcessName,
                "explorer",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool IsOwnedByOverlay(IntPtr windowHandle)
    {
        IntPtr current = windowHandle;
        for (int depth = 0; current != IntPtr.Zero && depth < 8; depth++)
        {
            if (current == _hwnd)
            {
                return true;
            }

            current = OSInterop.GetWindow(current, OSInterop.GwOwner);
        }

        return false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RestoreHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        _hiddenLineIndices.Clear();
        foreach (var region in _cardHitRegions)
        {
            region.Card.Visibility = Visibility.Visible;
        }

        CardContextMenu.Visibility = Visibility.Collapsed;
        ContextMenuCanvas.IsHitTestVisible = false;
    }

    private void RenderTranslatedBoxes()
    {
        ResultCanvas.Children.Clear();

        _cardHitRegions.Clear();
        for (int lineIndex = 0; lineIndex < _lines.Count; lineIndex++)
        {
            TranslatedLine line = _lines[lineIndex];
            int cardLineIndex = lineIndex;
            TranslatedLine cardLine = line;
            var (leftDip, topDip, widthDip, heightDip) = OverlayLayoutHelper.PhysicalToDip(
                line.BoundingBox,
                _overlayBounds,
                _screenInfo.DpiScaleX,
                _screenInfo.DpiScaleY);

            double sourceLineHeightDip = heightDip / Math.Max(1, line.SourceLineCount);
            double estimatedFontSize = OverlayLayoutHelper.CalculateEstimatedFontSize(sourceLineHeightDip);

            Border card = new()
            {
                Background = CreateBackgroundBrush(line),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"] ?? new SolidColorBrush(Windows.UI.Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Width = Math.Max(24, widthDip),
                MinHeight = Math.Max(18, heightDip),
            };

            TextBlock textBlock = new()
            {
                Text = line.TranslatedText,
                Foreground = CreateForegroundBrush(line),
                FontSize = estimatedFontSize,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.PointerPressed += Card_PointerPressed;
            card.PointerMoved += Card_PointerMoved;
            card.PointerReleased += Card_PointerReleased;
            card.PointerCanceled += Card_PointerCanceled;
            card.RightTapped += (_, args) =>
            {
                args.Handled = true;
                ShowCardContextMenu(card, cardLineIndex, cardLine);
            };
            card.Child = textBlock;

            Canvas.SetLeft(card, leftDip);
            Canvas.SetTop(card, topDip);
            _initialAppearance[lineIndex] = (
                textBlock.Text,
                GetBrushColor(card.Background),
                GetBrushColor(textBlock.Foreground),
                estimatedFontSize,
                leftDip,
                topDip,
                card.Width,
                card.MinHeight);
            _translatedTexts[lineIndex] = line.TranslatedText;

            ResultCanvas.Children.Add(card);
            _cardHitRegions.Add((line.BoundingBox, card));
        }
    }

    private void OriginalTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuLine is null ||
            _contextMenuCard is null ||
            _contextMenuLineIndex < 0 ||
            _contextMenuCard.Child is not TextBlock textBlock)
        {
            return;
        }

        ResourceLoader resources = ResourceLoader.GetForViewIndependentUse();
        if (_showingOriginalText.Remove(_contextMenuLineIndex))
        {
            textBlock.Text = _translatedTexts[_contextMenuLineIndex];
            SetOriginalTextButtonState(resources, showOriginalText: true);
        }
        else
        {
            _showingOriginalText.Add(_contextMenuLineIndex);
            textBlock.Text = _contextMenuLine.OriginalText;
            SetOriginalTextButtonState(resources, showOriginalText: false);
        }

        SetOriginalAllTextButtonState(resources, _showingOriginalText.Count != _lines.Count);
        textBlock.InvalidateMeasure();
        _contextMenuCard.InvalidateMeasure();
    }

    private void ShowCardContextMenu(Border card, int lineIndex, TranslatedLine line)
    {
        _contextMenuLineIndex = lineIndex;
        _contextMenuLine = line;
        _contextMenuCard = card;

        SelectLanguage(CardSourceLanguageComboBox, _sourceLanguage);
        SelectLanguage(CardTargetLanguageComboBox, _targetLanguage);
        ResourceLoader resources = ResourceLoader.GetForViewIndependentUse();
        SetOriginalTextButtonState(resources, !_showingOriginalText.Contains(lineIndex));
        SetOriginalAllTextButtonState(resources, _showingOriginalText.Count != _lines.Count);

        PositionContextMenu(card);
        CardContextMenu.Visibility = Visibility.Visible;
        ContextMenuCanvas.IsHitTestVisible = true;

        _isInitializingColorPickers = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (card.Background is SolidColorBrush backgroundBrush)
                {
                    BackgroundColorPicker.Color = backgroundBrush.Color;
                }

                if (card.Child is TextBlock textBlock &&
                    textBlock.Foreground is SolidColorBrush foregroundBrush)
                {
                    TextColorPicker.Color = foregroundBrush.Color;
                }
            }
            finally
            {
                _isInitializingColorPickers = false;
            }
        });
    }

    private void PositionContextMenu(Border card)
    {
        Canvas.SetLeft(CardContextMenu, Math.Max(8, Canvas.GetLeft(card)));
        Canvas.SetTop(CardContextMenu, Math.Max(8, Canvas.GetTop(card) + Math.Max(card.ActualHeight, card.MinHeight) + 4));
    }

    private void HideCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuLineIndex >= 0 && _contextMenuLineIndex < _cardHitRegions.Count)
        {
            _hiddenLineIndices.Add(_contextMenuLineIndex);
            _cardHitRegions[_contextMenuLineIndex].Card.Visibility = Visibility.Collapsed;
        }

        CardContextMenu.Visibility = Visibility.Collapsed;
        ContextMenuCanvas.IsHitTestVisible = false;
        _isInitializingColorPickers = false;
    }

    private void OriginalAllTextButton_Click(object sender, RoutedEventArgs e)
    {
        bool showOriginalText = _showingOriginalText.Count != _lines.Count;
        for (int lineIndex = 0; lineIndex < _cardHitRegions.Count; lineIndex++)
        {
            if (_cardHitRegions[lineIndex].Card.Child is not TextBlock textBlock)
            {
                continue;
            }

            if (showOriginalText)
            {
                _showingOriginalText.Add(lineIndex);
                textBlock.Text = _lines[lineIndex].OriginalText;
            }
            else
            {
                _showingOriginalText.Remove(lineIndex);
                textBlock.Text = _translatedTexts[lineIndex];
            }

            textBlock.InvalidateMeasure();
            _cardHitRegions[lineIndex].Card.InvalidateMeasure();
        }

        ResourceLoader resources = ResourceLoader.GetForViewIndependentUse();
        SetOriginalAllTextButtonState(resources, !showOriginalText);
        if (_contextMenuLineIndex >= 0)
        {
            SetOriginalTextButtonState(resources, !_showingOriginalText.Contains(_contextMenuLineIndex));
        }
    }

    private void EditCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetActiveCard(out Border card) ||
            card.Child is not TextBlock textBlock)
        {
            return;
        }

        _editingOriginalText = textBlock.Text;
        _editingCard = card;
        _editingTextBox = new TextBox
        {
            Text = textBlock.Text,
            Foreground = textBlock.Foreground,
            FontSize = textBlock.FontSize,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        _editingTextBox.KeyDown += EditingTextBox_KeyDown;
        _editingTextBox.LostFocus += EditingTextBox_LostFocus;
        card.Child = _editingTextBox;
        int exStyle = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlExStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlExStyle, exStyle & ~OSInterop.WsExNoActivate);
        _ = OSInterop.SetForegroundWindow(_hwnd);
        _editingTextBox.Focus(FocusState.Programmatic);
        _editingTextBox.SelectAll();
        Logger.LogInfo($"Started editing overlay text for line {_contextMenuLineIndex}.");
    }

    private void CopyCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetActiveCard(out Border card))
        {
            Logger.LogWarning("Ignored copy because no active card was selected.");
            return;
        }

        string text = card.Child switch
        {
            TextBlock textBlock => textBlock.Text,
            TextBox textBox => textBox.Text,
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
        Logger.LogInfo($"Copied overlay text for line {_contextMenuLineIndex} to the clipboard.");
    }

    private void EditingTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitEdit();
            e.Handled = true;
        }
    }

    private void EditingTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitEdit();
    }

    private void CommitEdit()
    {
        if (_editingCard is null || _editingTextBox is null)
        {
            return;
        }

        TextBlock textBlock = new()
        {
            Text = _editingTextBox.Text,
            Foreground = _editingTextBox.Foreground,
            FontSize = _editingTextBox.FontSize,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _editingCard.Child = textBlock;
        if (_contextMenuLineIndex >= 0 && !_showingOriginalText.Contains(_contextMenuLineIndex))
        {
            _translatedTexts[_contextMenuLineIndex] = textBlock.Text;
        }

        Logger.LogInfo($"Committed edited overlay text for line {_contextMenuLineIndex}.");
        ClearEditState();
    }

    private void CancelEdit()
    {
        if (_editingCard is null || _editingTextBox is null)
        {
            return;
        }

        TextBlock textBlock = new()
        {
            Text = _editingOriginalText,
            Foreground = _editingTextBox.Foreground,
            FontSize = _editingTextBox.FontSize,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _editingCard.Child = textBlock;
        Logger.LogInfo($"Cancelled editing overlay text for line {_contextMenuLineIndex}.");
        ClearEditState();
    }

    private void ClearEditState()
    {
        if (_editingTextBox is not null)
        {
            _editingTextBox.KeyDown -= EditingTextBox_KeyDown;
            _editingTextBox.LostFocus -= EditingTextBox_LostFocus;
        }

        _editingTextBox = null;
        _editingCard = null;
        _editingOriginalText = string.Empty;

        int exStyle = OSInterop.GetWindowLong(_hwnd, OSInterop.GwlExStyle);
        _ = OSInterop.SetWindowLong(_hwnd, OSInterop.GwlExStyle, exStyle | OSInterop.WsExNoActivate);
    }

    private void BackgroundColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_isInitializingColorPickers && TryGetActiveCard(out Border card))
        {
            card.Background = new SolidColorBrush(args.NewColor);
            card.InvalidateMeasure();
            Logger.LogInfo($"Applied overlay background color to line {_contextMenuLineIndex}: alpha={args.NewColor.A}.");
        }
        else if (!_isInitializingColorPickers)
        {
            Logger.LogWarning("Ignored overlay background color change because no active card was selected.");
        }
    }

    private void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_isInitializingColorPickers && TryGetActiveCard(out Border card))
        {
            if (card.Child is TextBlock textBlock)
            {
                textBlock.Foreground = new SolidColorBrush(args.NewColor);
                textBlock.InvalidateMeasure();
                Logger.LogInfo($"Applied overlay text color to line {_contextMenuLineIndex}.");
            }
            else if (card.Child is TextBox textBox)
            {
                textBox.Foreground = new SolidColorBrush(args.NewColor);
                textBox.InvalidateMeasure();
            }
        }
        else if (!_isInitializingColorPickers)
        {
            Logger.LogWarning("Ignored overlay text color change because no active card was selected.");
        }
    }

    private void DecreaseFontSizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetActiveCard(out Border card) && TryGetTextSizeElement(card, out TextBlock? textBlock, out TextBox? textBox))
        {
            double fontSize = textBlock?.FontSize ?? textBox!.FontSize;
            fontSize = Math.Max(9, fontSize - 1);
            if (textBlock is not null)
            {
                textBlock.FontSize = fontSize;
                textBlock.InvalidateMeasure();
            }
            else
            {
                textBox!.FontSize = fontSize;
                textBox.InvalidateMeasure();
            }

            card.InvalidateMeasure();
            Logger.LogInfo($"Decreased overlay font size for line {_contextMenuLineIndex} to {fontSize}.");
        }
        else
        {
            Logger.LogWarning("Ignored decrease font size because no active card was selected.");
        }
    }

    private void IncreaseFontSizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetActiveCard(out Border card) && TryGetTextSizeElement(card, out TextBlock? textBlock, out TextBox? textBox))
        {
            double fontSize = textBlock?.FontSize ?? textBox!.FontSize;
            fontSize = Math.Min(48, fontSize + 1);
            if (textBlock is not null)
            {
                textBlock.FontSize = fontSize;
                textBlock.InvalidateMeasure();
            }
            else
            {
                textBox!.FontSize = fontSize;
                textBox.InvalidateMeasure();
            }

            card.InvalidateMeasure();
            Logger.LogInfo($"Increased overlay font size for line {_contextMenuLineIndex} to {fontSize}.");
        }
        else
        {
            Logger.LogWarning("Ignored increase font size because no active card was selected.");
        }
    }

    private void RestoreInitialButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCard is not null && ReferenceEquals(_editingCard, _contextMenuCard))
        {
            CancelEdit();
        }

        if (_contextMenuCard is not null &&
            _initialAppearance.TryGetValue(_contextMenuLineIndex, out var initial))
        {
            Border card = _contextMenuCard;
            card.Background = new SolidColorBrush(initial.Background);
            card.Width = initial.Width;
            card.MinHeight = initial.MinHeight;
            TextBlock textBlock = new()
            {
                Text = initial.Text,
                Foreground = new SolidColorBrush(initial.Foreground),
                FontSize = initial.FontSize,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            card.Child = textBlock;
            Canvas.SetLeft(card, initial.Left);
            Canvas.SetTop(card, initial.Top);
            card.Visibility = Visibility.Visible;
            textBlock.InvalidateMeasure();
            card.InvalidateMeasure();
            _hiddenLineIndices.Remove(_contextMenuLineIndex);
            _showingOriginalText.Remove(_contextMenuLineIndex);
            _translatedTexts[_contextMenuLineIndex] = initial.Text;
            SetOriginalTextButtonState(ResourceLoader.GetForViewIndependentUse(), showOriginalText: true);
            SetOriginalAllTextButtonState(
                ResourceLoader.GetForViewIndependentUse(),
                _showingOriginalText.Count != _lines.Count);
            PositionContextMenu(card);

            Logger.LogInfo($"Restored initial overlay text, appearance, and position for line {_contextMenuLineIndex}.");
        }
        else
        {
            Logger.LogWarning($"Could not restore initial overlay state for line {_contextMenuLineIndex}: no active card snapshot.");
        }
    }

    private async void ApplyOverallLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_retranslateAll is null)
        {
            return;
        }

        string sourceLanguage = GetSelectedLanguage(OverallSourceLanguageComboBox, "auto");
        string targetLanguage = GetSelectedLanguage(OverallTargetLanguageComboBox, "en-US");
        CloseContextMenu();
        Close();
        await _retranslateAll(sourceLanguage, targetLanguage);
    }

    private async void ApplyCardLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_retranslateLine is null ||
            _contextMenuLine is null ||
            !TryGetActiveCard(out Border card) ||
            _contextMenuLineIndex < 0)
        {
            return;
        }

        string sourceLanguage = GetSelectedLanguage(CardSourceLanguageComboBox, "auto");
        string targetLanguage = GetSelectedLanguage(CardTargetLanguageComboBox, "en-US");
        int cardLineIndex = _contextMenuLineIndex;
        TranslationLine sourceLine = new(
            _contextMenuLine.OriginalText,
            _contextMenuLine.BoundingBox,
            _contextMenuLine.Confidence,
            _contextMenuLine.PolygonVertices,
            _contextMenuLine.SourceLineCount);

        CloseContextMenu();
        TranslationResult result = await _retranslateLine(sourceLine, sourceLanguage, targetLanguage);
        if (!result.Success || result.Lines.Count == 0)
        {
            Logger.LogWarning($"Failed to retranslate overlay line {cardLineIndex}: {result.ErrorMessage}");
            return;
        }

        if (card.Child is TextBlock textBlock)
        {
            _translatedTexts[cardLineIndex] = result.Lines[0].TranslatedText;
            if (!_showingOriginalText.Contains(cardLineIndex))
            {
                textBlock.Text = _translatedTexts[cardLineIndex];
            }

            textBlock.InvalidateMeasure();
            card.InvalidateMeasure();
        }
    }

    private static string GetSelectedLanguage(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag)
            ? tag
            : fallback;
    }

    private void SetOriginalTextButtonState(ResourceLoader resources, bool showOriginalText)
    {
        string contentKey = showOriginalText
            ? "OriginalTextButton/Content"
            : "TranslatedTextButton/Content";
        string accessibleNameKey = showOriginalText
            ? "OriginalTextButton/AutomationProperties/Name"
            : "TranslatedTextButton/AutomationProperties/Name";
        string tooltipKey = showOriginalText
            ? "OriginalTextButton/ToolTipService/ToolTip"
            : "TranslatedTextButton/ToolTipService/ToolTip";

        OriginalTextButton.Content = resources.GetString(contentKey);
        AutomationProperties.SetName(OriginalTextButton, resources.GetString(accessibleNameKey));
        ToolTipService.SetToolTip(OriginalTextButton, resources.GetString(tooltipKey));
    }

    private void SetOriginalAllTextButtonState(ResourceLoader resources, bool showOriginalText)
    {
        string contentKey = showOriginalText
            ? "OriginalAllTextButton/Content"
            : "TranslatedAllTextButton/Content";
        string accessibleNameKey = showOriginalText
            ? "OriginalAllTextButton/AutomationProperties/Name"
            : "TranslatedAllTextButton/AutomationProperties/Name";
        string tooltipKey = showOriginalText
            ? "OriginalAllTextButton/ToolTipService/ToolTip"
            : "TranslatedAllTextButton/ToolTipService/ToolTip";

        OriginalAllTextButton.Content = resources.GetString(contentKey);
        AutomationProperties.SetName(OriginalAllTextButton, resources.GetString(accessibleNameKey));
        ToolTipService.SetToolTip(OriginalAllTextButton, resources.GetString(tooltipKey));
    }

    private static void SelectLanguage(ComboBox comboBox, string language)
    {
        foreach (object item in comboBox.Items)
        {
            if (item is ComboBoxItem comboBoxItem &&
                (string.Equals(comboBoxItem.Tag as string, language, StringComparison.OrdinalIgnoreCase) ||
                 language.StartsWith($"{comboBoxItem.Tag}-", StringComparison.OrdinalIgnoreCase)))
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }
    }

    private void CloseContextMenu()
    {
        CardContextMenu.Visibility = Visibility.Collapsed;
        ContextMenuCanvas.IsHitTestVisible = false;
        _contextMenuCard = null;
        _contextMenuLine = null;
        _contextMenuLineIndex = -1;
    }

    private static Windows.UI.Color GetBrushColor(Brush brush)
    {
        return brush is SolidColorBrush solidBrush
            ? solidBrush.Color
            : Windows.UI.Color.FromArgb(255, 30, 30, 30);
    }

    private static bool TryGetTextSizeElement(Border card, out TextBlock? textBlock, out TextBox? textBox)
    {
        textBlock = card.Child as TextBlock;
        textBox = card.Child as TextBox;
        return textBlock is not null || textBox is not null;
    }

    private bool TryGetActiveCard(out Border card)
    {
        card = null!;
        if (_contextMenuCard is null || _contextMenuCard.Visibility != Visibility.Visible)
        {
            return false;
        }

        card = _contextMenuCard;
        return true;
    }

    private void Card_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border card || !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(ResultCanvas);
        _dragCard = card;
        _dragPointerId = point.PointerId;
        _dragStart = point.Position;
        _dragLeft = Canvas.GetLeft(card);
        _dragTop = Canvas.GetTop(card);
        card.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Card_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _dragCard) || e.Pointer.PointerId != _dragPointerId)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(ResultCanvas);
        Canvas.SetLeft(_dragCard, Math.Max(0, _dragLeft + point.Position.X - _dragStart.X));
        Canvas.SetTop(_dragCard, Math.Max(0, _dragTop + point.Position.Y - _dragStart.Y));
        e.Handled = true;
    }

    private void Card_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndCardDrag(e.Pointer);
    }

    private void Card_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndCardDrag(e.Pointer);
    }

    private void EndCardDrag(Pointer pointer)
    {
        if (_dragCard is not null)
        {
            _dragCard.ReleasePointerCapture(pointer);
        }

        _dragCard = null;
        _dragPointerId = 0;
    }

    private void HideMatchingCardsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuLine is not null)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (string.Equals(_lines[i].OriginalText, _contextMenuLine.OriginalText, StringComparison.Ordinal))
                {
                    _hiddenLineIndices.Add(i);
                    _cardHitRegions[i].Card.Visibility = Visibility.Collapsed;
                }
            }
        }

        CardContextMenu.Visibility = Visibility.Collapsed;
        ContextMenuCanvas.IsHitTestVisible = false;
    }

    private void RestoreContextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        RestoreHiddenButton_Click(sender, e);
        CardContextMenu.Visibility = Visibility.Collapsed;
        ContextMenuCanvas.IsHitTestVisible = false;
    }

    private static Brush CreateBackgroundBrush(TranslatedLine line)
    {
        return line.OverlayBackgroundColorArgb.HasValue
            ? new SolidColorBrush(ToColor(line.OverlayBackgroundColorArgb.Value))
            : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] ??
              new SolidColorBrush(Windows.UI.Color.FromArgb(235, 30, 30, 30));
    }

    private static Brush CreateForegroundBrush(TranslatedLine line)
    {
        return line.OverlayForegroundColorArgb.HasValue
            ? new SolidColorBrush(ToColor(line.OverlayForegroundColorArgb.Value))
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] ??
              new SolidColorBrush(Colors.White);
    }

    private static Windows.UI.Color ToColor(uint argb)
    {
        return Windows.UI.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);
    }

    private void PositionToolbar()
    {
        var (regionLeftDip, regionTopDip, _, _) = OverlayLayoutHelper.PhysicalToDip(
            _capturedRegion,
            _overlayBounds,
            _screenInfo.DpiScaleX,
            _screenInfo.DpiScaleY);

        double toolbarLeft = Math.Max(16, regionLeftDip);
        double toolbarTop = Math.Max(16, regionTopDip - 42);

        FloatingToolbar.Margin = new Thickness(toolbarLeft, toolbarTop, 0, 0);
    }
}
