# Threading and Window Management Migration

Based on patterns from the ImageResizer migration.

## Dispatcher → DispatcherQueue

### API Mapping

| WPF | WinUI 3 |
|-----|---------|
| `Dispatcher.Invoke(Action)` | `DispatcherQueue.TryEnqueue(Action)` |
| `Dispatcher.BeginInvoke(Action)` | `DispatcherQueue.TryEnqueue(Action)` |
| `Dispatcher.Invoke(DispatcherPriority, Action)` | `DispatcherQueue.TryEnqueue(DispatcherQueuePriority, Action)` |
| `Dispatcher.CheckAccess()` | `DispatcherQueue.HasThreadAccess` |
| `Dispatcher.VerifyAccess()` | Check `DispatcherQueue.HasThreadAccess` (no exception-throwing method) |

### Priority Mapping

WinUI 3 has only 3 levels: `High`, `Normal`, `Low`.

| WPF `DispatcherPriority` | WinUI 3 `DispatcherQueuePriority` |
|-------------------------|----------------------------------|
| `Send` | `High` |
| `Normal` / `Input` / `Loaded` / `Render` / `DataBind` | `Normal` |
| `Background` / `ContextIdle` / `ApplicationIdle` / `SystemIdle` | `Low` |

### Pattern: Global DispatcherQueue Access (from ImageResizer)

WPF provided `Application.Current.Dispatcher` globally. WinUI 3 requires explicit storage:

```csharp
// Store DispatcherQueue at app startup
private static DispatcherQueue _uiDispatcherQueue;

public static void InitializeDispatcher()
{
    _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
}
```

Usage with thread-check pattern (from `Settings.Reload()`):
```csharp
var currentDispatcher = DispatcherQueue.GetForCurrentThread();
if (currentDispatcher != null)
{
    // Already on UI thread
    ReloadCore(jsonSettings);
}
else if (_uiDispatcherQueue != null)
{
    // Dispatch to UI thread
    _uiDispatcherQueue.TryEnqueue(() => ReloadCore(jsonSettings));
}
else
{
    // Fallback (e.g., CLI mode, no UI)
    ReloadCore(jsonSettings);
}
```

### Pattern: DispatcherQueue in ViewModels (from ProgressViewModel)

```csharp
public class ProgressViewModel
{
    private readonly DispatcherQueue _dispatcherQueue;

    public ProgressViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    private void OnProgressChanged(double progress)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            Progress = progress;
            // other UI updates...
        });
    }
}
```

### Pattern: Async Dispatch (await)

```csharp
// WPF
await this.Dispatcher.InvokeAsync(() => { /* UI work */ });

// WinUI 3 (using TaskCompletionSource)
var tcs = new TaskCompletionSource();
this.DispatcherQueue.TryEnqueue(() =>
{
    try { /* UI work */ tcs.SetResult(); }
    catch (Exception ex) { tcs.SetException(ex); }
});
await tcs.Task;
```

### C++/WinRT Threading

| Old API | New API |
|---------|---------|
| `winrt::resume_foreground(CoreDispatcher)` | `wil::resume_foreground(DispatcherQueue)` |
| `CoreDispatcher.RunAsync()` | `DispatcherQueue.TryEnqueue()` |

Add `Microsoft.Windows.ImplementationLibrary` NuGet for `wil::resume_foreground`.

---

## Window Management

### Prefer `WindowEx` over bare `Window`

> **For top-level windows in WinUI 3 modules, default to `WinUIEx.WindowEx` or an existing PowerToys base derived from it, not bare `Window`.** Use `Microsoft.PowerToys.Common.UI.Controls.Window.TransparentWindow` when its transient-overlay behavior applies. Most rows in the table below say "No — use `AppWindow`…", which pushes windowing logic into code-behind. `WindowEx` restores many of these as **XAML properties**, so you declare windowing in XAML instead of hand-writing `AppWindow`/`OverlappedPresenter` code. Only drop to the raw `AppWindow`/presenter calls shown later for behavior `WindowEx` does not expose.

PowerToys centrally manages the WinUIEx version. Reference the package without specifying one:

```xml
<PackageReference Include="WinUIEx" />
```

Declare a regular top-level window as `WindowEx` and set properties inline:

```xml
<winuiex:WindowEx
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:winuiex="using:WinUIEx"
    MinWidth="480"
    MinHeight="320"
    IsMaximizable="False"
    IsResizable="False"
    IsTitleBarVisible="False">
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>
    <Grid>
        <!-- content -->
    </Grid>
</winuiex:WindowEx>
```

These WPF `Window` members become XAML properties on `WindowEx` (no code-behind):

| WPF `Window` | `WindowEx` (XAML unless noted) |
|--------------|-------------------------------|
| `MinWidth` / `MinHeight` | `MinWidth` / `MinHeight` |
| `Width` / `Height` | `Width` / `Height` |
| `ResizeMode="NoResize"` | `IsResizable="False"` |
| `WindowStyle` min/max buttons | `IsMaximizable` / `IsMinimizable` |
| `WindowState` | `WindowState` (`Normal`/`Minimized`/`Maximized`) |
| `Topmost` | `IsAlwaysOnTop` |
| `ShowInTaskbar` | `IsShownInSwitchers` (Alt-Tab visibility) |
| custom / hidden title bar | `IsTitleBarVisible` |
| backdrop | `<Window.SystemBackdrop><MicaBackdrop/></Window.SystemBackdrop>` |
| `WindowStartupLocation="CenterScreen"` | `CenterOnScreen()` (method) |
| persist size/position across sessions | `PersistenceId` (string) |

Regular-window examples in the repo: `src/modules/imageresizer/ui/ImageResizerXAML/MainWindow.xaml`, `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml`, `src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml`, `src/modules/AdvancedPaste/AdvancedPaste/AdvancedPasteXAML/MainWindow.xaml`, `src/modules/MeasureTool/MeasureToolUI/MeasureToolXAML/MainWindow.xaml`. For a derived-base overlay example, see `src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/MainWindow.xaml`.

### WPF Window vs WinUI 3 Window

| Feature | WPF `Window` | WinUI 3 `Window` |
|---------|-------------|------------------|
| Base class | `ContentControl` → `DependencyObject` | **NOT** a control, NOT a `DependencyObject` |
| `Resources` property | Yes | No — use root container's `Resources` |
| `DataContext` property | Yes | No — use root `Page`/`UserControl` |
| `VisualStateManager` | Yes | No — use inside child controls |
| `Load`/`Unload` events | Yes | No |
| `SizeToContent` | Yes (`Height`/`Width`/`WidthAndHeight`) | No — must implement manually |
| `WindowState` (min/max/normal) | Yes | No — use `AppWindow.Presenter` |
| `WindowStyle` | Yes | No — use `AppWindow` title bar APIs |
| `ResizeMode` | Yes | No — use `AppWindow.Presenter` |
| `WindowStartupLocation` | Yes | No — calculate manually |
| `Icon` | `Window.Icon` | `AppWindow.SetIcon()` |
| `Title` | `Window.Title` | `AppWindow.Title` (or `Window.Title`) |
| Size (Width/Height) | Yes | No — use `AppWindow.Resize()` |
| Position (Left/Top) | Yes | No — use `AppWindow.Move()` |
| `IsDefault`/`IsCancel` on buttons | Yes | No — handle Enter/Escape in code-behind |

### Getting AppWindow from Window

```csharp
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

IntPtr hwnd = WindowNative.GetWindowHandle(window);
WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
```

### Pattern: SizeToContent Replacement (from ImageResizer)

WinUI 3 has no `SizeToContent`. ImageResizer implemented a manual equivalent:

```csharp
private void SizeToContent()
{
    if (Content is not FrameworkElement content)
        return;

    // Measure desired content size
    content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    var desiredHeight = content.DesiredSize.Height + WindowChromeHeight + Padding;

    // Account for DPI scaling
    var scaleFactor = Content.XamlRoot.RasterizationScale;
    var pixelHeight = (int)(desiredHeight * scaleFactor);
    var pixelWidth = (int)(WindowWidth * scaleFactor);

    // Resize via AppWindow
    var hwnd = WindowNative.GetWindowHandle(this);
    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
    var appWindow = AppWindow.GetFromWindowId(windowId);
    appWindow.Resize(new Windows.Graphics.SizeInt32(pixelWidth, pixelHeight));
}
```

**Key details:**
- `WindowChromeHeight` ≈ 32px for the title bar
- Must multiply by `RasterizationScale` for DPI-aware sizing
- Call `SizeToContent()` after page navigation or content changes
- Unsubscribe previous event handlers before subscribing new ones to avoid memory leaks

### Window Positioning (Center Screen)

```csharp
var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
var centerX = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
var centerY = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
```

### Window State (Minimize/Maximize)

```csharp
(appWindow.Presenter as OverlappedPresenter)?.Maximize();
(appWindow.Presenter as OverlappedPresenter)?.Minimize();
(appWindow.Presenter as OverlappedPresenter)?.Restore();
```

### Title Bar Customization

```csharp
// Extend content into title bar
this.ExtendsContentIntoTitleBar = true;
this.SetTitleBar(AppTitleBar); // AppTitleBar is a XAML element

// Or via AppWindow API
if (AppWindowTitleBar.IsCustomizationSupported())
{
    var titleBar = appWindow.TitleBar;
    titleBar.ExtendsContentIntoTitleBar = true;
    titleBar.ButtonBackgroundColor = Colors.Transparent;
}
```

### Tracking the Main Window

```csharp
public partial class App : Application
{
    public static Window MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
```

### ContentDialog Requires XamlRoot

```csharp
var dialog = new ContentDialog
{
    Title = "Confirm",
    Content = "Are you sure?",
    PrimaryButtonText = "Yes",
    CloseButtonText = "No",
    XamlRoot = this.Content.XamlRoot  // REQUIRED
};
var result = await dialog.ShowAsync();
```

### File Pickers Require HWND

```csharp
var picker = new FileOpenPicker();
picker.FileTypeFilter.Add(".jpg");

// REQUIRED for desktop apps
var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

var file = await picker.PickSingleFileAsync();
```

### Window Close Handling

```csharp
// WPF
protected override void OnClosing(CancelEventArgs e) { e.Cancel = true; this.Hide(); }

// WinUI 3
this.AppWindow.Closing += (s, e) => { e.Cancel = true; this.AppWindow.Hide(); };
```

---

## Custom Entry Point (DISABLE_XAML_GENERATED_MAIN)

ImageResizer uses a custom `Program.cs` entry point instead of the WinUI 3 auto-generated `Main`. This is needed for:
- CLI mode (process files without showing UI)
- Custom initialization before the WinUI 3 App starts
- Single-instance enforcement

### Setup

In `.csproj`:
```xml
<DefineConstants>DISABLE_XAML_GENERATED_MAIN,TRACE</DefineConstants>
```

Create `Program.cs`:
```csharp
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            // CLI mode — no UI
            return RunCli(args);
        }

        // GUI mode
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}
```

### WPF App Constructor Removal

WPF modules often created `new App()` to initialize the WPF `Application` and get `Application.Current.Dispatcher`. This is no longer needed — the WinUI 3 `Application.Start()` handles this.

```csharp
// DELETE (WPF pattern):
_imageResizerApp = new App();
// REPLACE with: Store DispatcherQueue explicitly (see Global DispatcherQueue Access above)
```
