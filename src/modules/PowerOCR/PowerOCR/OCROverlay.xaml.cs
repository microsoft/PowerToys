// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using PowerOCR.Helpers;
using PowerOCR.Settings;
using PowerOCR.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Windows.System;

namespace PowerOCR;

public sealed partial class OCROverlay : Window
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
    private bool isSingleLineMode;
    private bool isTableMode;
    private const double ActiveOpacity = 0.4;
    private UserSettings? userSettings;
    private RectInt32 screenRect;
    private double rasterizationScale;
    private byte[]? _capturedScreenshotBytes;

    // Tooltip strings loaded from resources for x:Bind
    internal string SingleLineTooltip { get; }

    internal string TableTooltip { get; }

    internal string CancelTooltip { get; }

    private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? _resourceLoader;

    private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? ResourceLoaderInstance
    {
        get
        {
            if (_resourceLoader == null)
            {
                try
                {
                    _resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader("PowerToys.PowerOCR.pri");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create ResourceLoader: {ex}");
                }
            }

            return _resourceLoader;
        }
    }

    private static string GetLocalizedString(string resourceKey)
    {
        try
        {
            return ResourceLoaderInstance?.GetString(resourceKey) ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load resource '{resourceKey}': {ex}");
            return string.Empty;
        }
    }

    public OCROverlay(RectInt32 screenRectParam, double rasterizationScaleParam)
    {
        screenRect = screenRectParam;
        rasterizationScale = rasterizationScaleParam;

        SingleLineTooltip = GetLocalizedString("ResultTextSingleLineShortcut/Text");
        TableTooltip = GetLocalizedString("ResultTextTableShortcut/Text");
        CancelTooltip = GetLocalizedString("CancelShortcut/Text");

        // Capture screenshot BEFORE window becomes visible (otherwise we capture our own white window)
        // Store as raw bytes — converting to BitmapImage requires async, which deadlocks on UI thread
        System.Drawing.Rectangle screenDrawingRect = new(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);
        using var bmp = new System.Drawing.Bitmap(screenDrawingRect.Width, screenDrawingRect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(screenDrawingRect.Left, screenDrawingRect.Top, 0, 0, bmp.Size, System.Drawing.CopyPixelOperation.SourceCopy);
        }

        using var ms = new System.IO.MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        _capturedScreenshotBytes = ms.ToArray();

        InitializeComponent();

        // Transparent window background to prevent white flash during load and close
        this.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();

        userSettings = new UserSettings(new ThrottledActionInvoker());

        // Configure transparent overlay window
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
        }

        // Position and size window using physical pixels
        AppWindow.MoveAndResize(screenRect);

        PopulateLanguageMenu();

        RootGrid.Loaded += Window_Loaded;
        this.Closed += Window_Closed;
    }

    private void PopulateLanguageMenu()
    {
        string? selectedLanguageName = userSettings?.PreferredLanguage.Value;

        if (string.IsNullOrEmpty(selectedLanguageName))
        {
            selectedLanguage = ImageMethods.GetOCRLanguage();
            selectedLanguageName = selectedLanguage?.NativeName;
        }

        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        int count = 0;

        foreach (Language language in possibleOcrLanguages)
        {
            LanguagesComboBox.Items.Add(new ComboBoxItem { Content = EnsureStartUpper(language.NativeName), Tag = language });
            if (language.NativeName.Equals(selectedLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                selectedLanguage = language;
                LanguagesComboBox.SelectedIndex = count;
            }

            count++;
        }

        isComboBoxReady = true;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        double width = screenRect.Width / rasterizationScale;
        double height = screenRect.Height / rasterizationScale;
        FullWindow.Rect = new Rect(0, 0, width, height);

        // Wire up keyboard events on the root grid
        RootGrid.KeyDown += MainWindow_KeyDown;
        RootGrid.KeyUp += MainWindow_KeyUp;

        // Set pre-captured screenshot as background (async to avoid UI thread deadlock)
        if (_capturedScreenshotBytes != null)
        {
            var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using var stream = new System.IO.MemoryStream(_capturedScreenshotBytes);
            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
            BackgroundImage.Source = bitmapImage;
            _capturedScreenshotBytes = null;
        }

        RootGrid.Visibility = Visibility.Visible;
        DarkOverlayPath.Opacity = ActiveOpacity;
        TopButtonsStackPanel.Visibility = Visibility.Visible;

        // Set focus so keyboard events fire
        RootGrid.IsTabStop = true;
        RootGrid.Focus(FocusState.Programmatic);

#if DEBUG
        if (AppWindow.Presenter is OverlappedPresenter debugPresenter)
        {
            debugPresenter.IsAlwaysOnTop = false;
        }
#endif
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        RootGrid.Visibility = Visibility.Collapsed;
        BackgroundImage.Source = null;
        App.UntrackOverlay(this);
    }

    private void MainWindow_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.LeftShift:
            case VirtualKey.RightShift:
                isShiftDown = false;
                clickedPoint = new Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                break;
            default:
                break;
        }
    }

    private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        WindowUtilities.OcrOverlayKeyDown(e.Key);
    }

    private void RegionClickCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var crossCursor = OSInterop.LoadCursor(IntPtr.Zero, OSInterop.IDC_CROSS);
        OSInterop.SetCursor(crossCursor);
    }

    private void RegionClickCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RegionClickCanvas);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
        RegionClickCanvas.CapturePointer(e.Pointer);

        CursorClipper.ClipCursor(this);
        clickedPoint = point.Position;
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
        var borderColor = Windows.UI.Color.FromArgb(255, 40, 118, 126);
        selectBorder.BorderBrush = new SolidColorBrush(borderColor);
        RegionClickCanvas.Children.Add(selectBorder);
        Canvas.SetLeft(selectBorder, clickedPoint.X);
        Canvas.SetTop(selectBorder, clickedPoint.Y);

        IsSelecting = true;
    }

    private void RegionClickCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        OSInterop.SetCursor(OSInterop.LoadCursor(IntPtr.Zero, OSInterop.IDC_CROSS));

        if (!IsSelecting)
        {
            return;
        }

        var point = e.GetCurrentPoint(RegionClickCanvas);
        Point movingPoint = point.Position;

        var keyState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        bool isShiftPressed = (keyState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

        if (isShiftPressed)
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

    private async void RegionClickCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (IsSelecting == false)
        {
            return;
        }

        TopButtonsStackPanel.Visibility = Visibility.Visible;
        IsSelecting = false;

        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleasePointerCaptures();

        double scale = this.Content.XamlRoot.RasterizationScale;

        var point = e.GetCurrentPoint(RegionClickCanvas);
        Point movingPoint = point.Position;
        movingPoint = new Point(movingPoint.X * scale, movingPoint.Y * scale);

        movingPoint = new Point(Math.Round(movingPoint.X), Math.Round(movingPoint.Y));

        double xDimScaled = Canvas.GetLeft(selectBorder) * scale;
        double yDimScaled = Canvas.GetTop(selectBorder) * scale;

        System.Drawing.Rectangle regionScaled = new(
            (int)xDimScaled,
            (int)yDimScaled,
            (int)(selectBorder.Width * scale),
            (int)(selectBorder.Height * scale));

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
            DarkOverlayPath.Opacity = 0;
            Logger.LogInfo($"Getting clicked word, {selectedLanguage?.LanguageTag}");
            grabbedText = await ImageMethods.GetClickedWord(this, new Point(xDimScaled, yDimScaled), selectedLanguage);
        }
        else
        {
            if (isTableMode)
            {
                Logger.LogInfo($"Getting region as table, {selectedLanguage?.LanguageTag}");
                grabbedText = await OcrExtensions.GetRegionsTextAsTableAsync(this, regionScaled, selectedLanguage);
            }
            else
            {
                Logger.LogInfo($"Standard region capture, {selectedLanguage?.LanguageTag}");
                grabbedText = await ImageMethods.GetRegionsText(this, regionScaled, selectedLanguage);

                if (isSingleLineMode)
                {
                    Logger.LogInfo($"Making grabbed text single line");
                    grabbedText = grabbedText.MakeStringSingleLine();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(grabbedText))
        {
            DarkOverlayPath.Opacity = ActiveOpacity;
            return;
        }

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(grabbedText);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Clipboard.SetContent exception: {ex}");
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

        int selection = languageComboBox.SelectedIndex;
        selectedLanguage = (languageComboBox.SelectedItem as ComboBoxItem)?.Tag as Language;

        if (selectedLanguage == null)
        {
            return;
        }

        Logger.LogInfo($"Changed language to {selectedLanguage?.LanguageTag}");

        switch (selection)
        {
            case 0:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number1);
                break;
            case 1:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number2);
                break;
            case 2:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number3);
                break;
            case 3:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number4);
                break;
            case 4:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number5);
                break;
            case 5:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number6);
                break;
            case 6:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number7);
                break;
            case 7:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number8);
                break;
            case 8:
                WindowUtilities.OcrOverlayKeyDown(VirtualKey.Number9);
                break;
            default:
                break;
        }
    }

    private void SingleLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.OcrOverlayKeyDown(VirtualKey.S, isActive);
    }

    private void TableToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.OcrOverlayKeyDown(VirtualKey.T, isActive);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllOCROverlays();
        Helpers.SettingsDeepLink.OpenSettings();
    }

    private static bool CheckIfCheckingOrUnchecking(object? sender)
    {
        if (sender is ToggleButton tb && tb.IsChecked is not null)
        {
            return tb.IsChecked.Value;
        }

        return false;
    }

    internal void KeyPressed(VirtualKey key, bool? isActive)
    {
        switch (key)
        {
            case VirtualKey.S:
                if (isActive is null)
                {
                    isSingleLineMode = !isSingleLineMode;
                }
                else
                {
                    isSingleLineMode = isActive.Value;
                }

                SingleLineToggleButton.IsChecked = isSingleLineMode;
                break;
            case VirtualKey.T:
                if (isActive is null)
                {
                    isTableMode = !isTableMode;
                }
                else
                {
                    isTableMode = isActive.Value;
                }

                TableToggleButton.IsChecked = isTableMode;

                break;
            case VirtualKey.Number1:
            case VirtualKey.Number2:
            case VirtualKey.Number3:
            case VirtualKey.Number4:
            case VirtualKey.Number5:
            case VirtualKey.Number6:
            case VirtualKey.Number7:
            case VirtualKey.Number8:
            case VirtualKey.Number9:
                int numberPressed = (int)key - (int)VirtualKey.Number0;
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

    public RectInt32 GetScreenRect()
    {
        return screenRect;
    }

    public System.Drawing.Rectangle GetScreenRectangle()
    {
        return new System.Drawing.Rectangle(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);
    }

    private string EnsureStartUpper(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var inputArray = input.ToCharArray();
        inputArray[0] = char.ToUpper(inputArray[0], CultureInfo.CurrentCulture);
        return new string(inputArray);
    }
}
