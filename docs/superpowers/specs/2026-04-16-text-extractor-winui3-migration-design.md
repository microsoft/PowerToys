# Text Extractor: WPF to WinUI 3 Full Migration Design

## Overview

Migrate the PowerToys Text Extractor (PowerOCR) module from WPF to WinUI 3. This is a full migration — including the transparent fullscreen overlay window — following patterns validated in the ImageResizer migration.

**Module location:** `src/modules/PowerOCR/PowerOCR/`
**Settings UI:** Already on WinUI 3 (`src/settings-ui/Settings.UI/SettingsXAML/Views/PowerOcrPage.xaml`) — no changes needed.

## Current Architecture

- **Project:** Single `WinExe`, `net9.0-windows`, `<UseWPF>true`, `<UseWindowsForms>true`
- **UI:** One transparent fullscreen `Window` (`OCROverlay.xaml`) per monitor for region selection + OCR
- **MVVM:** No ViewModel for overlay — all logic in code-behind
- **Localization:** `.resx` with `{x:Static p:Resources.Key}` (8 strings)
- **Imaging:** Mixed `System.Drawing` (screenshots) + WPF `BitmapImage`/`TransformedBitmap` (scaling) + WinRT `SoftwareBitmap`/`OcrEngine` (OCR)
- **Multi-monitor:** `System.Windows.Forms.Screen.AllScreens` + P/Invoke DPI helpers
- **Runner integration:** Named events via `NativeEventWaiter`, Runner PID monitoring, GPO checks
- **P/Invoke:** ~80+ declarations in `OSInterop.cs` (window management, DPI, cursor, input)

### Source files (27 .cs + 2 .xaml)

| Role | Files |
|------|-------|
| Views | `App.xaml(.cs)`, `OCROverlay.xaml(.cs)` |
| Helpers | `ImageMethods.cs`, `OcrExtensions.cs`, `WindowUtilities.cs`, `WPFExtensionMethods.cs`, `CursorClipper.cs`, `OSInterop.cs`, `StringHelpers.cs`, `LanguageHelper.cs`, `WrappingStream.cs`, `ThrottledActionInvoker.cs`, `IThrottledActionInvoker.cs` |
| Keyboard | `EventMonitor.cs`, `KeyboardMonitor.cs`, `GlobalKeyboardHook.cs`, `GlobalKeyboardHookEventArgs.cs` |
| Models | `ResultTable.cs`, `ResultRow.cs`, `ResultColumn.cs`, `WordBorder.cs`, `NullAsyncResult.cs`, `NullWaitHandle.cs` |
| Settings | `UserSettings.cs`, `IUserSettings.cs`, `SettingItem<T>.cs` |
| Telemetry | `PowerOCRInvokedEvent.cs`, `PowerOCRCaptureEvent.cs`, `PowerOCRCancelledEvent.cs` |
| Resources | `Properties/Resources.resx`, `Properties/Resources.Designer.cs` |

## Migration Design

### 1. Project File (`PowerOCR.csproj`)

**Remove:**
- `<UseWPF>true</UseWPF>`
- `<UseWindowsForms>true</UseWindowsForms>`
- `<GenerateSatelliteAssembliesForCore>true</GenerateSatelliteAssembliesForCore>`
- `<PackageReference Include="System.ComponentModel.Composition" />`
- `<ProjectReference Include="Common.UI.csproj" />`
- `EmbeddedResource` / `Compile Update` entries for `Resources.resx` / `Resources.Designer.cs`

**Add:**
- `<UseWinUI>true</UseWinUI>`
- `<PackageReference Include="Microsoft.WindowsAppSDK" />`
- `<PackageReference Include="Microsoft.Windows.SDK.BuildTools" />`
- Keep `<PackageReference Include="System.Drawing.Common" />` (still needed for screenshot capture)

**Keep unchanged:**
- TFM: `net9.0-windows10.0.19041.0` (from Directory.Build.props)
- `OutputType: WinExe`
- Project references: `GPOWrapperProjection`, `PowerToys.Interop`, `Settings.UI.Library`
- CsWinRT props import

### 2. App.xaml + App.xaml.cs (Lifecycle)

**App.xaml — WinUI 3 structure:**
```xml
<Application x:Class="PowerOCR.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
            <FontFamily x:Key="SymbolThemeFontFamily">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>
            <!-- SubtleButtonStyle, SubtleToggleButtonStyle (rewritten with VSM) -->
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

**App.xaml.cs — key changes:**
- Extend `Microsoft.UI.Xaml.Application` instead of `System.Windows.Application`
- Static `DispatcherQueue` field stored at construction time
- `NativeEventWaiter` callbacks use `DispatcherQueue.TryEnqueue()` instead of `Dispatcher.Invoke()`
- `OnLaunched()` replaces `Application_Startup` — parse Runner PID, init settings, start event monitor
- Explicit shutdown: `Application.Current.Exit()` or process exit — no `ShutdownMode` equivalent
- Instance mutex, GPO check, Runner PID monitoring — logic stays the same, only threading API changes

### 3. OCROverlay Window (Core Migration)

#### 3a. Transparent Window Implementation

WinUI 3 `Window` doesn't have `AllowsTransparency`. Use this approach:

```csharp
// In OCROverlay constructor or Activated handler:
var presenter = AppWindow.Presenter as OverlappedPresenter;
presenter.IsAlwaysOnTop = true;
presenter.SetBorderAndTitleBar(false, false);
presenter.IsResizable = false;

// Transparent background via SystemBackdrop (WinAppSDK 1.6+)
this.SystemBackdrop = new TransparentTintBackdrop();
```

Window positioning via `AppWindow.MoveAndResize()` using `RectInt32` in physical pixels (already physical from `DisplayArea`).

#### 3b. XAML Conversion

**OCROverlay.xaml transforms:**

| WPF | WinUI 3 |
|-----|---------|
| `<Window ... AllowsTransparency="True" WindowStyle="None">` | `<Window>` — transparency + chrome in code-behind |
| `xmlns:p="clr-namespace:PowerOCR.Properties"` | Remove — use `x:Uid` |
| `{x:Static p:Resources.Key}` | `x:Uid="Key"` with `.Header`, `.Text`, `.Content` in `.resw` |
| `{DynamicResource BrushName}` | `{ThemeResource BrushName}` |
| `MouseDown="..."` / `MouseMove` / `MouseUp` | `PointerPressed` / `PointerMoved` / `PointerReleased` |
| `CaptureMouse()` | `CapturePointer(e.Pointer)` |
| `Canvas.ContextMenu` → `ContextMenu` | `Canvas.ContextFlyout` → `MenuFlyout` |
| `MenuItem IsCheckable="True"` | `ToggleMenuFlyoutItem` |
| `Separator` | `MenuFlyoutSeparator` |
| `ToggleButton Style="{DynamicResource SubtleToggleButtonStyle}"` | `Style="{ThemeResource SubtleToggleButtonStyle}"` |
| `ToolTip="..."` | `ToolTipService.ToolTip="..."` |
| `Viewbox > Image` | Same — both exist in WinUI 3 |
| `Canvas` + `CombinedGeometry` | Same — both exist in WinUI 3 |
| `RectangleGeometry` | Same |
| `SolidColorBrush` | Same |

**Window root structure change:**
Since WinUI 3 `Window` is not a `DependencyObject`, resources and `DataContext` cannot be set on it. Move the root `<Grid>` content pattern — this is fine since `OCROverlay` already has a `<Grid>` as root with no resources on the `<Window>` element.

#### 3c. Code-Behind Conversion

**Constructor:**
```csharp
// Before (WPF):
public OCROverlay(System.Drawing.Rectangle screenRectangleParam, DpiScale dpiScaleParam)

// After (WinUI 3):
public OCROverlay(Windows.Graphics.RectInt32 screenRect, double rasterizationScale)
```
- Use `AppWindow.MoveAndResize(screenRect)` for positioning
- Store `rasterizationScale` for coordinate transforms

**Mouse → Pointer events:**
```csharp
// Before:
private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.LeftButton != MouseButtonState.Pressed) return;
    RegionClickCanvas.CaptureMouse();
    clickedPoint = e.GetPosition(this);
    ...
}

// After:
private void RegionClickCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    if (!e.GetCurrentPoint(RegionClickCanvas).Properties.IsLeftButtonPressed) return;
    RegionClickCanvas.CapturePointer(e.Pointer);
    clickedPoint = e.GetCurrentPoint(RegionClickCanvas).Position;
    ...
}
```

**DPI Transform (MouseUp):**
```csharp
// Before:
Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
movingPoint.X *= m.M11;

// After:
double scale = Content.XamlRoot.RasterizationScale;
movingPoint.X *= scale;
movingPoint.Y *= scale;
```

**Keyboard:**
```csharp
// Before: System.Windows.Input.Key, System.Windows.Input.Keyboard.Modifiers
// After: Windows.System.VirtualKey, check via InputKeyboardSource or pointer properties
```

**Clipboard:**
```csharp
// Before:
System.Windows.Clipboard.SetText(grabbedText);

// After:
var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
dataPackage.SetText(grabbedText);
Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
```

**HWND access:**
```csharp
// Before:
IntPtr hwnd = new WindowInteropHelper(this).Handle;

// After:
IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
```

### 4. Imaging (ImageMethods.cs)

**Keep as-is:**
- `Graphics.CopyFromScreen()` — framework-agnostic screenshot capture
- `PadImage()` — pure `System.Drawing`, no WPF types
- `GetRegionAsBitmap()` — pure `System.Drawing`
- `ExtractText()` OCR pipeline: `MemoryStream` → `BitmapDecoder` → `SoftwareBitmap` → `OcrEngine` (already WinRT)

**Replace:**
- `ScaleBitmapUniform()` — currently uses WPF `BitmapImage` + `ScaleTransform`. Rewrite with `System.Drawing`:
  ```csharp
  public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
  {
      int newWidth = (int)(passedBitmap.Width * scale);
      int newHeight = (int)(passedBitmap.Height * scale);
      Bitmap scaled = new(newWidth, newHeight, passedBitmap.PixelFormat);
      using Graphics g = Graphics.FromImage(scaled);
      g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
      g.DrawImage(passedBitmap, 0, 0, newWidth, newHeight);
      return scaled;
  }
  ```

- `BitmapToImageSource()` — currently returns WPF `BitmapImage`. Rewrite for WinUI 3:
  ```csharp
  internal static async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage> BitmapToImageSourceAsync(Bitmap bitmap)
  {
      using MemoryStream ms = new();
      bitmap.Save(ms, ImageFormat.Bmp);
      ms.Position = 0;
      var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
      var stream = ms.AsRandomAccessStream();
      await bitmapImage.SetSourceAsync(stream);
      return bitmapImage;
  }
  ```

- `BitmapSourceToBitmap()` — delete entirely (WPF-only helper, no longer needed after `ScaleBitmapUniform` rewrite)

- `GetWindowBoundsImage()` — return type changes to `Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage>`, becomes async

- `GetOCRLanguage()` — replace `InputLanguageManager.Current.CurrentInputLanguage.Name` with `Windows.Globalization.Language` API. Replace `MessageBox.Show()` with no-op or logging (overlay context, no dialog).

**Remove:**
- All `using System.Windows.Media`, `using System.Windows.Media.Imaging` references
- `BitmapSourceToBitmap()` method
- WPF `BitmapImage.BeginInit()/.EndInit()/.Freeze()` patterns

### 5. OcrExtensions.cs

- Remove `using System.Windows.Media` (used for `VisualTreeHelper.GetDpi()`)
- `GetRegionsTextAsTableAsync()`: Replace `DpiScale dpiScale = VisualTreeHelper.GetDpi(passedWindow)` with `double scale = passedWindow.Content.XamlRoot.RasterizationScale`
- Adapt `ResultTable.ParseOcrResultIntoWordBorders()` and `GetWordsAsTable()` to accept `double` scale instead of WPF `DpiScale`

### 6. Resources / Localization

**Convert `.resx` → `.resw`:**

Create `Strings/en-us/Resources.resw` with these entries:

| Name | Value |
|------|-------|
| `Cancel.Text` | Cancel |
| `CancelMenuItem.Text` | Cancel |
| `CancelShortcut.Text` | Cancel (Esc) |
| `ResultTextSingleLine.Text` | Format result as a single line |
| `ResultTextSingleLineShortcut.Text` | Format result as a single line (S) |
| `ResultTextTable.Text` | Format result as a table |
| `ResultTextTableShortcut.Text` | Format result as a table (T) |
| `SelectedLang.Text` | Selected language |
| `Settings.Text` | Settings |

Plus `.Header`, `.Content` variants for controls that need them (MenuFlyoutItem uses `.Text`, Button uses automation name via code-behind or `x:Uid`).

**XAML references:**
- `Header="{x:Static p:Resources.ResultTextSingleLine}"` → `x:Uid="ResultTextSingleLine"` (and add `ResultTextSingleLine.Text` to `.resw`)
- `AutomationProperties.Name="{x:Static p:Resources.SelectedLang}"` → `x:Uid="SelectedLang"` (with `SelectedLang.AutomationProperties.Name` in `.resw`)

**Code-behind references:**
- Where strings are needed in code (e.g., programmatic menu items for languages), use `ResourceLoader.GetForViewIndependentUse().GetString("Key")`

**Delete:**
- `Properties/Resources.resx`
- `Properties/Resources.Designer.cs`

### 7. Multi-Monitor / DPI (`WindowUtilities.cs`)

**Before:**
```csharp
foreach (Screen screen in Screen.AllScreens)
{
    DpiScale dpiScale = screen.GetDpi();
    OCROverlay overlay = new(screen.Bounds, dpiScale);
    overlay.Show();
}
```

**After:**
```csharp
var displays = DisplayArea.FindAll();
foreach (var display in displays)
{
    var workArea = display.OuterBounds;
    var screenRect = new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height);
    var overlay = new OCROverlay(screenRect, /* rasterizationScale from display */);
    overlay.Activate();
    App.TrackOverlay(overlay);
}
```

**Window tracking:**
- Replace `Application.Current.Windows` (WPF) with `static List<OCROverlay>` in `App`
- `IsOCROverlayCreated()` → check the tracked list
- `CloseAllOCROverlays()` → iterate and close from the tracked list
- `OcrOverlayKeyDown()` → iterate the tracked list

**Delete:**
- `WPFExtensionMethods.cs` — `GetAbsolutePosition()` and `GetDpi()` are WPF-only DPI helpers, no longer needed

### 8. Window Activation (`WindowUtilities.ActivateWindow`)

**Before:**
```csharp
var handle = new WindowInteropHelper(window).Handle;
```

**After:**
```csharp
var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
```

P/Invoke calls (`GetForegroundWindow`, `AttachThreadInput`, `SetForegroundWindow`) stay the same — they're Win32 API, framework-agnostic.

### 9. Event Monitor / Keyboard

**EventMonitor.cs:**
- Constructor param: `System.Windows.Threading.Dispatcher` → `Microsoft.UI.Dispatching.DispatcherQueue`
- `NativeEventWaiter.WaitForEventLoop` already accepts a dispatcher-like callback — verify it can accept `DispatcherQueue` or adapt the call

**KeyboardMonitor.cs, GlobalKeyboardHook.cs:**
- Pure P/Invoke (`SetWindowsHookEx`, `CallNextHookEx`) — no WPF dependency
- `System.Windows.Input.Key` references → `Windows.System.VirtualKey` where used in overlay communication

### 10. Custom Styles → VisualStateManager

**SubtleButtonStyle** — rewrite with VSM:
```xml
<Style x:Key="SubtleButtonStyle" TargetType="Button"
       BasedOn="{StaticResource DefaultButtonStyle}">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Border"
                    Padding="{TemplateBinding Padding}"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="4">
                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                            <VisualState x:Name="Normal"/>
                            <VisualState x:Name="PointerOver">
                                <Storyboard>
                                    <ObjectAnimationUsingKeyFrames Storyboard.TargetName="Border" Storyboard.TargetProperty="Background">
                                        <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource SubtleFillColorSecondaryBrush}"/>
                                    </ObjectAnimationUsingKeyFrames>
                                    <ObjectAnimationUsingKeyFrames Storyboard.TargetName="Border" Storyboard.TargetProperty="BorderBrush">
                                        <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource SubtleFillColorSecondaryBrush}"/>
                                    </ObjectAnimationUsingKeyFrames>
                                </Storyboard>
                            </VisualState>
                            <VisualState x:Name="Pressed">
                                <Storyboard>
                                    <ObjectAnimationUsingKeyFrames Storyboard.TargetName="Border" Storyboard.TargetProperty="Background">
                                        <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource SubtleFillColorTertiaryBrush}"/>
                                    </ObjectAnimationUsingKeyFrames>
                                    <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentPresenter" Storyboard.TargetProperty="Foreground">
                                        <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource TextFillColorSecondaryBrush}"/>
                                    </ObjectAnimationUsingKeyFrames>
                                </Storyboard>
                            </VisualState>
                            <VisualState x:Name="Disabled">
                                <Storyboard>
                                    <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentPresenter" Storyboard.TargetProperty="Foreground">
                                        <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource TextFillColorDisabledBrush}"/>
                                    </ObjectAnimationUsingKeyFrames>
                                </Storyboard>
                            </VisualState>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>
                    <ContentPresenter x:Name="ContentPresenter"
                        HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                        VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**SubtleToggleButtonStyle** — same pattern, with additional `Checked` visual state:
```xml
<VisualState x:Name="Checked">
    <Storyboard>
        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="Border" Storyboard.TargetProperty="Background">
            <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource AccentFillColorDefaultBrush}"/>
        </ObjectAnimationUsingKeyFrames>
        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentPresenter" Storyboard.TargetProperty="Foreground">
            <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}"/>
        </ObjectAnimationUsingKeyFrames>
    </Storyboard>
</VisualState>
```

### 11. Installer / WiX

- Remove satellite assembly entries (`PowerToys.PowerOCR.resources.dll`) from `installer/PowerToysSetupVNext/Resources.wxs`
- WinUI 3 uses `.pri` files for localization — these are handled automatically by the build
- Run `generateAllFileComponents.ps1` to regenerate the component list
- Verify signing config only includes files under the correct output path

### 12. Models with WPF Dependencies

**`ResultTable.cs`** — uses `System.Windows.DpiScale`, `System.Windows.Controls.Canvas`, `System.Windows.Media.SolidColorBrush`, `System.Windows.Rect`:
- `DpiScale` parameter → replace with `double` (single uniform scale factor)
- `using Rect = System.Windows.Rect` → `using Rect = Windows.Foundation.Rect`
- `SolidColorBrush` (line 573, used for debug drawing on Canvas) → `Microsoft.UI.Xaml.Media.SolidColorBrush` with `Microsoft.UI.ColorHelper`
- `System.Windows.Controls.Canvas` usage (debug border drawing) → `Microsoft.UI.Xaml.Controls.Canvas`

**`WordBorder.cs`** — uses `System.Windows.Rect` for `AsRect()` and `IntersectsWith()`:
- `using System.Windows` → `using Windows.Foundation`
- `Rect` type changes from `System.Windows.Rect` → `Windows.Foundation.Rect`
- `Rect.IntersectsWith()` — verify API compatibility (WinRT `Rect` has no `IntersectsWith`; implement manually or use helper)

### 13. Files Unchanged (No WPF dependency)

These files have zero `System.Windows` usage and require no changes:

- `Models/ResultColumn.cs`, `ResultRow.cs`, `NullAsyncResult.cs`, `NullWaitHandle.cs`
- `Helpers/StringHelpers.cs`, `LanguageHelper.cs`, `WrappingStream.cs`, `ThrottledActionInvoker.cs`, `IThrottledActionInvoker.cs`
- `Helpers/CursorClipper.cs` (pure P/Invoke)
- `Helpers/OSInterop.cs` (pure P/Invoke — may need minor type adjustments for `IntPtr` on .NET 9)
- `Settings/UserSettings.cs`, `IUserSettings.cs`, `SettingItem<T>.cs`
- `Telemetry/PowerOCRInvokedEvent.cs`, `PowerOCRCaptureEvent.cs`, `PowerOCRCancelledEvent.cs`
- `Keyboard/GlobalKeyboardHook.cs`, `GlobalKeyboardHookEventArgs.cs`

### 14. Files to Delete

- `Properties/Resources.resx`
- `Properties/Resources.Designer.cs`
- `Helpers/WPFExtensionMethods.cs`

### 15. Files to Create

- `Strings/en-us/Resources.resw`

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `TransparentTintBackdrop` visual artifacts on multi-monitor | Low | Fallback: Win32 `WS_EX_NOREDIRECTIONBITMAP` via P/Invoke |
| `BitmapToImageSourceAsync` perf regression (now async) | Low | Measure; if too slow, use `WriteableBitmap` with direct pixel copy |
| `CombinedGeometry` clip behavior difference | Very Low | Identical API surface in WinUI 3 |
| Missing theme resources at runtime | Medium | Ensure `XamlControlsResources` is first merged dictionary |
| `NativeEventWaiter` doesn't accept `DispatcherQueue` | Medium | Adapt overload or wrap callback with `TryEnqueue` |
| Installer satellite DLL errors | Medium | Run `generateAllFileComponents.ps1`; test installer build early |

## Out of Scope

- Settings UI page (`PowerOcrPage.xaml`) — already WinUI 3
- OOBE page (`OobePowerOCR.xaml`) — already WinUI 3
- Settings.UI.Library (`PowerOcrViewModel.cs`) — shared library, no changes needed
- Adding CommunityToolkit.Mvvm source generators — the overlay has no ViewModel, not worth adding
- Migrating screenshot capture from `System.Drawing` to `Windows.Graphics.Capture` — different feature, not part of this migration
