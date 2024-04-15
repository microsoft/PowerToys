// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;
using PowerOCR.Settings;
using PowerOCR.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace PowerOCR;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class OCROverlay : Window
{
    private bool isShiftDown;
    private Point clickedPoint;
    private Point shiftPoint;
    private Border selectBorder = new();
    private Language? selectedLanguage;

    private bool IsSelecting { get; set; }

    private double selectLeft;
    private double selectTop;

    private double xShiftDelta;
    private double yShiftDelta;
    private bool isComboBoxReady;
    private const double ActiveOpacity = 0.4;
    private readonly UserSettings userSettings = new(new ThrottledActionInvoker());
    private System.Drawing.Rectangle screenRectangle;
    private DpiScale dpiScale;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    public OCROverlay(System.Drawing.Rectangle screenRectangleParam, DpiScale dpiScaleParam)
    {
        screenRectangle = screenRectangleParam;
        dpiScale = dpiScaleParam;

        Left = screenRectangle.Left;
        Top = screenRectangle.Top;
        Width = screenRectangle.Width / dpiScale.DpiScaleX;
        Height = screenRectangle.Height / dpiScale.DpiScaleY;

        InitializeComponent();

        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, Wpf.Ui.Controls.WindowBackdropType.None);

        PopulateLanguageMenu();
    }

    private void PopulateLanguageMenu()
    {
        string? selectedLanguageName = userSettings.PreferredLanguage.Value;

        // build context menu
        if (string.IsNullOrEmpty(selectedLanguageName))
        {
            selectedLanguage = ImageMethods.GetOCRLanguage();
            selectedLanguageName = selectedLanguage?.DisplayName;
        }

        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        int count = 0;

        foreach (Language language in possibleOcrLanguages)
        {
            MenuItem menuItem = new() { Header = language.NativeName, Tag = language, IsCheckable = true };
            menuItem.IsChecked = language.DisplayName.Equals(selectedLanguageName, StringComparison.Ordinal);
            LanguagesComboBox.Items.Add(language);
            if (language.DisplayName.Equals(selectedLanguageName, StringComparison.Ordinal))
            {
                selectedLanguage = language;
                LanguagesComboBox.SelectedIndex = count;
            }

            menuItem.Click += LanguageMenuItem_Click;
            CanvasContextMenu.Items.Add(menuItem);
            count++;
        }

        isComboBoxReady = true;
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MenuItem menuItem = (MenuItem)sender;
        foreach (var item in CanvasContextMenu.Items)
        {
            if (item is MenuItem menuItemLoop)
            {
                menuItemLoop.IsChecked = item.Equals(menuItem);
            }
        }

        selectedLanguage = menuItem.Tag as Language;
        LanguagesComboBox.SelectedItem = selectedLanguage;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FullWindow.Rect = new Rect(0, 0, Width, Height);
        KeyDown += MainWindow_KeyDown;
        KeyUp += MainWindow_KeyUp;

        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        BackgroundBrush.Opacity = ActiveOpacity;

        TopButtonsStackPanel.Visibility = Visibility.Visible;

#if DEBUG
        Topmost = false;
#endif
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        // The first move puts it on the correct monitor, which triggers WM_DPICHANGED
        // The +1/-1 coerces WPF to update Window.Top/Left/Width/Height in the second move
        MoveWindow(hwnd, (int)(screenRectangle.Left + 1), (int)screenRectangle.Top, (int)(screenRectangle.Width - 1), (int)screenRectangle.Height, false);
        MoveWindow(hwnd, (int)screenRectangle.Left, (int)screenRectangle.Top, (int)screenRectangle.Width, (int)screenRectangle.Height, true);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        BackgroundImage.Source = null;
        BackgroundImage.UpdateLayout();

        KeyDown -= MainWindow_KeyDown;
        KeyUp -= MainWindow_KeyUp;

        Loaded -= Window_Loaded;
        Unloaded -= Window_Unloaded;

        RegionClickCanvas.MouseDown -= RegionClickCanvas_MouseDown;
        RegionClickCanvas.MouseUp -= RegionClickCanvas_MouseUp;
        RegionClickCanvas.MouseMove -= RegionClickCanvas_MouseMove;
    }

    private void MainWindow_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftShift:
            case Key.RightShift:
                isShiftDown = false;
                clickedPoint = new Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                break;
            default:
                break;
        }
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        WindowUtilities.OcrOverlayKeyDown(e.Key);
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
        RegionClickCanvas.CaptureMouse();

        CursorClipper.ClipCursor(this);
        clickedPoint = e.GetPosition(this);
        selectBorder.Height = 1;
        selectBorder.Width = 1;

        try
        {
            RegionClickCanvas.Children.Remove(selectBorder);
        }
        catch (Exception)
        {
        }

        selectBorder.BorderThickness = new Thickness(2);
        Color borderColor = Color.FromArgb(255, 40, 118, 126);
        selectBorder.BorderBrush = new SolidColorBrush(borderColor);
        _ = RegionClickCanvas.Children.Add(selectBorder);
        Canvas.SetLeft(selectBorder, clickedPoint.X);
        Canvas.SetTop(selectBorder, clickedPoint.Y);

        IsSelecting = true;
    }

    private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsSelecting)
        {
            return;
        }

        Point movingPoint = e.GetPosition(this);

        if (System.Windows.Input.Keyboard.Modifiers == ModifierKeys.Shift)
        {
            if (!isShiftDown)
            {
                shiftPoint = movingPoint;
                selectLeft = Canvas.GetLeft(selectBorder);
                selectTop = Canvas.GetTop(selectBorder);
            }

            isShiftDown = true;
            xShiftDelta = movingPoint.X - shiftPoint.X;
            yShiftDelta = movingPoint.Y - shiftPoint.Y;

            double leftValue = selectLeft + xShiftDelta;
            double topValue = selectTop + yShiftDelta;

            clippingGeometry.Rect = new Rect(
                new Point(leftValue, topValue),
                new Size(selectBorder.Width, selectBorder.Height));
            Canvas.SetLeft(selectBorder, leftValue - 1);
            Canvas.SetTop(selectBorder, topValue - 1);
            return;
        }

        isShiftDown = false;

        double left = Math.Min(clickedPoint.X, movingPoint.X);
        double top = Math.Min(clickedPoint.Y, movingPoint.Y);

        selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
        selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
        selectBorder.Height += 2;
        selectBorder.Width += 2;

        clippingGeometry.Rect = new Rect(
            new Point(left, top),
            new Size(selectBorder.Width - 2, selectBorder.Height - 2));
        Canvas.SetLeft(selectBorder, left - 1);
        Canvas.SetTop(selectBorder, top - 1);
    }

    private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (IsSelecting == false)
        {
            return;
        }

        TopButtonsStackPanel.Visibility = Visibility.Visible;
        IsSelecting = false;

        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleaseMouseCapture();
        Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

        Point movingPoint = e.GetPosition(this);
        movingPoint.X *= m.M11;
        movingPoint.Y *= m.M22;

        movingPoint.X = Math.Round(movingPoint.X);
        movingPoint.Y = Math.Round(movingPoint.Y);

        double xDimScaled = Canvas.GetLeft(selectBorder) * m.M11;
        double yDimScaled = Canvas.GetTop(selectBorder) * m.M22;

        System.Drawing.Rectangle regionScaled = new(
            (int)xDimScaled,
            (int)yDimScaled,
            (int)(selectBorder.Width * m.M11),
            (int)(selectBorder.Height * m.M22));

        string grabbedText;

        try
        {
            RegionClickCanvas.Children.Remove(selectBorder);
            clippingGeometry.Rect = new Rect(0, 0, 0, 0);
        }
        catch
        {
        }

        if (regionScaled.Width < 3 || regionScaled.Height < 3)
        {
            BackgroundBrush.Opacity = 0;
            Logger.LogInfo($"Getting clicked word, {selectedLanguage?.LanguageTag}");
            grabbedText = await ImageMethods.GetClickedWord(this, new Point(xDimScaled, yDimScaled), selectedLanguage);
        }
        else
        {
            if (TableMenuItem.IsChecked)
            {
                Logger.LogInfo($"Getting region as table, {selectedLanguage?.LanguageTag}");
                grabbedText = await OcrExtensions.GetRegionsTextAsTableAsync(this, regionScaled, selectedLanguage);
            }
            else
            {
                Logger.LogInfo($"Standard region capture, {selectedLanguage?.LanguageTag}");
                grabbedText = await ImageMethods.GetRegionsText(this, regionScaled, selectedLanguage);

                if (SingleLineMenuItem.IsChecked)
                {
                    Logger.LogInfo($"Making grabbed text single line");
                    grabbedText = grabbedText.MakeStringSingleLine();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(grabbedText))
        {
            BackgroundBrush.Opacity = ActiveOpacity;
            return;
        }

        try
        {
            Clipboard.SetText(grabbedText);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Clipboard.SetText exception: {ex}");
        }

        WindowUtilities.CloseAllOCROverlays();
        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCaptureEvent());
    }

    private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllOCROverlays();
        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCancelledEvent());
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageComboBox || !isComboBoxReady)
        {
            return;
        }

        // TODO: Set the preferred language based upon what was chosen here
        int selection = languageComboBox.SelectedIndex;
        selectedLanguage = languageComboBox.SelectedItem as Language;

        Logger.LogError($"Changed language to {selectedLanguage?.LanguageTag}");

        // Set the language in the context menu
        foreach (var item in CanvasContextMenu.Items)
        {
            if (item is MenuItem menuItemLoop)
            {
                menuItemLoop.IsChecked = menuItemLoop.Tag as Language == selectedLanguage;
            }
        }

        switch (selection)
        {
            case 0:
                WindowUtilities.OcrOverlayKeyDown(Key.D1);
                break;
            case 1:
                WindowUtilities.OcrOverlayKeyDown(Key.D2);
                break;
            case 2:
                WindowUtilities.OcrOverlayKeyDown(Key.D3);
                break;
            case 3:
                WindowUtilities.OcrOverlayKeyDown(Key.D4);
                break;
            case 4:
                WindowUtilities.OcrOverlayKeyDown(Key.D5);
                break;
            case 5:
                WindowUtilities.OcrOverlayKeyDown(Key.D6);
                break;
            case 6:
                WindowUtilities.OcrOverlayKeyDown(Key.D7);
                break;
            case 7:
                WindowUtilities.OcrOverlayKeyDown(Key.D8);
                break;
            case 8:
                WindowUtilities.OcrOverlayKeyDown(Key.D9);
                break;
            default:
                break;
        }
    }

    private void SingleLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.OcrOverlayKeyDown(Key.S, isActive);
    }

    private void TableToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.OcrOverlayKeyDown(Key.T, isActive);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllOCROverlays();
        SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.PowerOCR, false);
    }

    private static bool CheckIfCheckingOrUnchecking(object? sender)
    {
        if (sender is ToggleButton tb && tb.IsChecked is not null)
        {
            return tb.IsChecked.Value;
        }

        if (sender is MenuItem mi)
        {
            return mi.IsChecked;
        }

        return false;
    }

    internal void KeyPressed(Key key, bool? isActive)
    {
        switch (key)
        {
            // This case is handled in the WindowUtilities.OcrOverlayKeyDown
            // case Key.Escape:
            //     WindowUtilities.CloseAllFullscreenGrabs();
            //     break;
            case Key.S:
                if (isActive is null)
                {
                    SingleLineMenuItem.IsChecked = !SingleLineMenuItem.IsChecked;
                }
                else
                {
                    SingleLineMenuItem.IsChecked = isActive.Value;
                }

                // Possibly save this in settings later and remember this preference
                break;
            case Key.T:
                if (isActive is null)
                {
                    TableToggleButton.IsChecked = !TableToggleButton.IsChecked;
                }
                else
                {
                    TableToggleButton.IsChecked = isActive.Value;
                }

                break;
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
            case Key.D6:
            case Key.D7:
            case Key.D8:
            case Key.D9:
                int numberPressed = (int)key - 34; // D1 casts to 35, D2 to 36, etc.
                int numberOfLanguages = LanguagesComboBox.Items.Count;

                if (numberPressed <= numberOfLanguages
                    && numberPressed - 1 >= 0
                    && numberPressed - 1 != LanguagesComboBox.SelectedIndex
                    && isComboBoxReady)
                {
                    LanguagesComboBox.SelectedIndex = numberPressed - 1;
                }

                break;
            default:
                break;
        }
    }

    public System.Drawing.Rectangle GetScreenRectangle()
    {
        return screenRectangle;
    }
}
