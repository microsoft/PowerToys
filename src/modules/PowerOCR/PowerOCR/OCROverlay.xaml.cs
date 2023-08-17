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
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;
using PowerOCR.Models.Drawing;
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
    private MenuItem? cancelMenuItem;

    internal ScreenInfo? CurrentScreen
    {
        get;
        init;
    }

    internal DpiScale? CurrentScaling
    {
        get;
        init;
    }

    private bool IsSelecting { get; set; }

    private double selectLeft;
    private double selectTop;

    private double xShiftDelta;
    private double yShiftDelta;

    private const double ActiveOpacity = 0.4;

    public OCROverlay()
    {
        InitializeComponent();
    }

    private void PopulateLanguageMenu()
    {
        var userSettings = new UserSettings(new ThrottledActionInvoker());
        string? selectedLanguageName = userSettings.PreferredLanguage.Value;

        // build context menu
        if (string.IsNullOrEmpty(selectedLanguageName))
        {
            selectedLanguage = LanguageHelper.GetOCRLanguage();
            selectedLanguageName = selectedLanguage?.DisplayName;
        }

        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();
        foreach (Language language in possibleOcrLanguages)
        {
            MenuItem menuItem = new() { Header = language.NativeName, Tag = language, IsCheckable = true };
            menuItem.IsChecked = language.DisplayName.Equals(selectedLanguageName, StringComparison.Ordinal);
            if (language.DisplayName.Equals(selectedLanguageName, StringComparison.Ordinal))
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
            if (item is MenuItem menuItemLoop)
            {
                menuItemLoop.IsChecked = item.Equals(menuItem);
            }
        }

        selectedLanguage = menuItem.Tag as Language;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FullWindow.Rect = new Rect(0, 0, Width, Height);

        KeyDown += MainWindow_KeyDown;
        KeyUp += MainWindow_KeyUp;

        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        BackgroundBrush.Opacity = ActiveOpacity;

        PopulateLanguageMenu();
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

        if (cancelMenuItem is not null)
        {
            cancelMenuItem.Click -= CancelMenuItem_Click;
        }
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
            grabbedText = await ImageMethods.GetClickedWord(this, new Point(xDimScaled, yDimScaled), selectedLanguage);
        }
        else
        {
            grabbedText = await ImageMethods.GetRegionsText(this, regionScaled, selectedLanguage);
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
}
