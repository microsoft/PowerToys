// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    private bool IsSelecting { get; set; }

    private Border selectBorder = new Border();

    private DpiScale? dpiScale;

    private Point GetMousePos() => PointToScreen(Mouse.GetPosition(this));

    private Language? selectedLanguage = null;
    private MenuItem cancelMenuItem;

    private System.Windows.Forms.Screen? CurrentScreen
    {
        get;
        set;
    }

    private double selectLeft;
    private double selectTop;

    private double xShiftDelta;
    private double yShiftDelta;

    private const double ActiveOpacity = 0.4;

    public OCROverlay()
    {
        InitializeComponent();

        var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
        string? selectedLanguageName = userSettings.PreferredLanguage.Value;

        // build context menu
        if (string.IsNullOrEmpty(selectedLanguageName))
        {
            selectedLanguage = ImageMethods.GetOCRLanguage();
            selectedLanguageName = selectedLanguage?.DisplayName;
        }

        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();
        foreach (Language language in possibleOcrLanguages)
        {
            MenuItem menuItem = new MenuItem() { Header = language.NativeName, Tag = language, IsCheckable = true };
            menuItem.IsChecked = language.DisplayName.Equals(selectedLanguageName);
            if (language.DisplayName.Equals(selectedLanguageName))
            {
                selectedLanguage = language;
            }

            menuItem.Click += LanguageMenuItem_Click;
            CanvasContextMenu.Items.Add(menuItem);
        }

        CanvasContextMenu.Items.Add(new Separator());

        // ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse(); // resourceLoader.GetString("TextExtractor_Cancel")
        cancelMenuItem = new MenuItem() { Header = "cancel" };
        cancelMenuItem.Click += CancelMenuItem_Click;
        CanvasContextMenu.Items.Add(cancelMenuItem);
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MenuItem menuItem = (MenuItem)sender;
        foreach (var item in CanvasContextMenu.Items)
        {
            if (item is MenuItem)
            {
                MenuItem menuItemLoop = (MenuItem)item;
                menuItemLoop.IsChecked = item.Equals(menuItem);
            }
        }

        selectedLanguage = menuItem.Tag as Language;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        FullWindow.Rect = new Rect(0, 0, Width, Height);
        KeyDown += MainWindow_KeyDown;
        KeyUp += MainWindow_KeyUp;

        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        BackgroundBrush.Opacity = ActiveOpacity;
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        BackgroundImage.Source = null;
        BackgroundImage.UpdateLayout();

        CurrentScreen = null;
        dpiScale = null;

        KeyDown -= MainWindow_KeyDown;
        KeyUp -= MainWindow_KeyUp;

        Loaded -= Window_Loaded;
        Unloaded -= Window_Unloaded;

        RegionClickCanvas.MouseDown -= RegionClickCanvas_MouseDown;
        RegionClickCanvas.MouseUp -= RegionClickCanvas_MouseUp;
        RegionClickCanvas.MouseMove -= RegionClickCanvas_MouseMove;

        cancelMenuItem.Click -= CancelMenuItem_Click;
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
        switch (e.Key)
        {
            case Key.Escape:
                WindowUtilities.CloseAllOCROverlays();
                PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCancelledEvent());
                break;
            default:
                break;
        }
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        RegionClickCanvas.CaptureMouse();

        CursorClipper.ClipCursor(this);
        clickedPoint = e.GetPosition(this);
        selectBorder.Height = 1;
        selectBorder.Width = 1;

        dpiScale = VisualTreeHelper.GetDpi(this);

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

        var screens = System.Windows.Forms.Screen.AllScreens;
        System.Drawing.Point formsPoint = new System.Drawing.Point((int)clickedPoint.X, (int)clickedPoint.Y);
        foreach (var scr in screens)
        {
            if (scr.Bounds.Contains(formsPoint))
            {
                CurrentScreen = scr;
                break;
            }
        }

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

            if (CurrentScreen is not null && dpiScale is not null)
            {
                double currentScreenLeft = CurrentScreen.Bounds.Left; // Should always be 0
                double currentScreenRight = CurrentScreen.Bounds.Right / dpiScale.Value.DpiScaleX;
                double currentScreenTop = CurrentScreen.Bounds.Top; // Should always be 0
                double currentScreenBottom = CurrentScreen.Bounds.Bottom / dpiScale.Value.DpiScaleY;

                // this is giving issues on different monitors
                // leftValue = Math.Clamp(leftValue, currentScreenLeft, currentScreenRight - selectBorder.Width);
                // topValue = Math.Clamp(topValue, currentScreenTop, currentScreenBottom - selectBorder.Height);
            }

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

        IsSelecting = false;

        CurrentScreen = null;
        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleaseMouseCapture();
        Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

        Point mPt = GetMousePos();
        Point movingPoint = e.GetPosition(this);
        movingPoint.X *= m.M11;
        movingPoint.Y *= m.M22;

        movingPoint.X = Math.Round(movingPoint.X);
        movingPoint.Y = Math.Round(movingPoint.Y);

        double xDimScaled = Canvas.GetLeft(selectBorder) * m.M11;
        double yDimScaled = Canvas.GetTop(selectBorder) * m.M22;

        System.Drawing.Rectangle regionScaled = new System.Drawing.Rectangle(
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
            grabbedText = await ImageMethods.GetClickedWord(this, new Point(xDimScaled, yDimScaled), selectedLanguage);
        }
        else
        {
            grabbedText = await ImageMethods.GetRegionsText(this, regionScaled, selectedLanguage);
        }

        if (string.IsNullOrWhiteSpace(grabbedText) == false)
        {
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
    }

    private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllOCROverlays();
        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRCancelledEvent());
    }
}
