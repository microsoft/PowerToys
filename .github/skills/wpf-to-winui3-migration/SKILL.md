---
name: wpf-to-winui3-migration
description: Guide for migrating PowerToys modules from WPF to WinUI 3 (Windows App SDK). Use when asked to migrate WPF code, convert WPF XAML to WinUI, replace System.Windows namespaces with Microsoft.UI.Xaml, update Dispatcher to DispatcherQueue, replace DynamicResource with ThemeResource, migrate imaging APIs from System.Windows.Media.Imaging to Windows.Graphics.Imaging, convert WPF Window to WinUI Window, migrate .resx to .resw resources, migrate custom Observable/RelayCommand to CommunityToolkit.Mvvm source generators, handle WPF-UI (Lepo) to WinUI native control migration, or fix installer/build pipeline issues after migration. Keywords: WPF, WinUI, WinUI3, migration, porting, convert, namespace, XAML, Dispatcher, DispatcherQueue, imaging, BitmapImage, Window, ContentDialog, ThemeResource, DynamicResource, ResourceLoader, resw, resx, CommunityToolkit, ObservableProperty, WPF-UI, SizeToContent, AppWindow, SoftwareBitmap.
license: Complete terms in LICENSE.txt
---

# WPF to WinUI 3 Migration Skill

Migrate PowerToys modules from WPF (`System.Windows.*`) to WinUI 3 (`Microsoft.UI.Xaml.*` / Windows App SDK). Based on patterns validated in the ImageResizer module migration.

## When to Use This Skill

- Migrate a PowerToys module from WPF to WinUI 3
- Convert WPF XAML files to WinUI 3 XAML
- Replace `System.Windows` namespaces with `Microsoft.UI.Xaml`
- Migrate `Dispatcher` usage to `DispatcherQueue`
- Migrate custom `Observable`/`RelayCommand` to CommunityToolkit.Mvvm source generators
- Replace WPF-UI (Lepo) controls with native WinUI 3 controls
- Convert imaging code from `System.Windows.Media.Imaging` to `Windows.Graphics.Imaging`
- Handle WPF `Window` vs WinUI `Window` differences (sizing, positioning, SizeToContent)
- Migrate resource files from `.resx` to `.resw` with `ResourceLoader`
- Fix installer/build pipeline issues after WinUI 3 migration
- Update project files, NuGet packages, and signing config

## Prerequisites

- Visual Studio 2022 17.4+
- Windows App SDK NuGet package (`Microsoft.WindowsAppSDK`)
- .NET 8+ with `net8.0-windows10.0.19041.0` TFM
- Windows 10 1803+ (April 2018 Update or newer)

## Migration Strategy

### Phase-by-Phase Scope

Work on bounded problems, not the entire codebase at once. Each phase should compile before moving to the next.

1. **Project file** — Update TFM, NuGet packages, set `<UseWinUI>true</UseWinUI>`
2. **Data models and business logic** — No UI dependencies, migrate first
3. **MVVM framework** — Replace custom Observable/RelayCommand with CommunityToolkit.Mvvm
4. **Resource strings** — Migrate `.resx` → `.resw`, introduce `ResourceLoaderInstance`
5. **Services and utilities** — Replace `System.Windows` types, async-ify imaging code
6. **ViewModels** — Update Dispatcher usage, binding patterns
7. **Views/Pages** — Starting from leaf pages with fewest dependencies
8. **Main page / shell** — Last, since it depends on everything
9. **App.xaml / startup code** — Merge carefully (do NOT overwrite WinUI 3 boilerplate)
10. **Installer & build pipeline** — Update WiX, signing, build events
11. **Tests** — Adapt for WinUI 3 runtime, async patterns

### Migration Contract: Prohibited Patterns

These rules capture human judgment and must be applied consistently across every file. Do NOT deviate.

**Architecture prohibitions:**
- **Do NOT overwrite `App.xaml` / `App.xaml.cs`** — WinUI 3 has different lifecycle boilerplate. Merge resources and init code into the generated WinUI 3 App class.
- **Do NOT create Exe→WinExe `ProjectReference`** — Extract shared code to a Library project. Causes phantom build artifacts.
- **Do NOT instantiate services directly** — Use DI and CommunityToolkit.Mvvm patterns.
- **Do NOT create new `Window` classes** — PowerToys modules use a single Window with content navigation.
- **Do NOT omit `WindowsPackageType=None` and `WindowsAppSDKSelfContained=true`** — Both are mandatory in the csproj for every WinUI 3 module in PowerToys. Without them the app crashes at startup with `COMException: ClassFactory cannot supply requested class` because the WinUI 3 runtime DLLs are not found.

**XAML prohibitions:**
- **Do NOT use `{DynamicResource}`** — Replace with `{ThemeResource}` (theme-reactive) or `{StaticResource}`.
- **Do NOT use `{Binding}` in `Setter.Value`** — Not supported in WinUI 3. Use `{StaticResource}`.
- **Do NOT use `{x:Static}`** — Replace with `{x:Bind}`, `x:Uid`, or code-behind.
- **Do NOT use `{x:Type}`** — Not supported. Use `x:DataType` for DataTemplate, or code-behind.
- **Do NOT use `clr-namespace:`** — Replace with `using:` in all xmlns declarations.
- **Do NOT use `Style.Triggers` / `DataTrigger` / `EventTrigger`** — Replace with `VisualStateManager`.
- **Do NOT use `MultiBinding`** — Replace with `x:Bind` function binding or computed ViewModel property.
- **Do NOT use `Visibility="Hidden"`** — WinUI only has `Visible` and `Collapsed`. Use `Opacity="0"` if layout must be preserved.
- **Do NOT use `IsDefault` / `IsCancel`** — Use `AccentButtonStyle` for primary button; handle Enter/Escape in code-behind.
- **Do NOT omit `BasedOn` when overriding default styles** — Without it, your style replaces the entire default. Always use `BasedOn="{StaticResource DefaultButtonStyle}"` etc.
- **Do NOT omit `XamlControlsResources` as first merged dictionary** — It provides default Fluent styles. Without it, controls have no visual appearance.

**Code-behind prohibitions:**
- **Do NOT use `Application.Current.Dispatcher`** — Store `DispatcherQueue` in a static field explicitly.
- **Do NOT use `Window.Current`** — Not supported. Use a custom `App.Window` static property.
- **Do NOT put `DataContext`, `Resources`, or `VisualStateManager` on `Window`** — WinUI 3 `Window` is NOT a `DependencyObject`. Use a root `Page`/`UserControl`/`Grid`.
- **Do NOT use tunneling/preview events** (`PreviewMouseDown`, `PreviewKeyDown`) — WinUI has no tunneling. Use bubbling equivalents with `Handled` property or `AddHandler(handledEventsToo: true)`.

**Resource prohibitions:**
- **Do NOT use `Properties.Resources.MyString`** — Replace with `ResourceLoaderInstance.ResourceLoader.GetString("MyString")`.
- **Do NOT initialize `ResourceLoader`-dependent values as static fields** — Wrap in `Lazy<T>` or null-coalescing property.
- **Do NOT use `pack://` URIs** — Replace with `ms-appx:///` scheme.

## Quick Reference Tables

### Namespace Mapping

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `System.Windows` | `Microsoft.UI.Xaml` | Root namespace |
| `System.Windows.Controls` | `Microsoft.UI.Xaml.Controls` | Core controls |
| `System.Windows.Controls.Primitives` | `Microsoft.UI.Xaml.Controls.Primitives` | Low-level primitives |
| `System.Windows.Media` | `Microsoft.UI.Xaml.Media` | Brushes, transforms |
| `System.Windows.Media.Animation` | `Microsoft.UI.Xaml.Media.Animation` | Storyboard, animations |
| `System.Windows.Media.Imaging` | `Microsoft.UI.Xaml.Media.Imaging` (UI) / `Windows.Graphics.Imaging` (processing) | Split by purpose |
| `System.Windows.Media.Media3D` | **No equivalent** | Use Win2D or Composition APIs |
| `System.Windows.Shapes` | `Microsoft.UI.Xaml.Shapes` | Rectangle, Ellipse, Path |
| `System.Windows.Input` | `Microsoft.UI.Xaml.Input` | Pointer, keyboard, focus |
| `System.Windows.Data` | `Microsoft.UI.Xaml.Data` | Binding, IValueConverter |
| `System.Windows.Documents` | `Microsoft.UI.Xaml.Documents` | Limited — RichTextBlock + Paragraph |
| `System.Windows.Markup` | `Microsoft.UI.Xaml.Markup` | XAML parsing, markup extensions |
| `System.Windows.Automation` | `Microsoft.UI.Xaml.Automation` | Accessibility / UI Automation |
| `System.Windows.Navigation` | **No direct equivalent** | Use `Frame.Navigate()` |
| `System.Windows.Threading` | `Microsoft.UI.Dispatching` | Dispatcher → DispatcherQueue |
| `System.Windows.Interop` | `WinRT.Interop` / `Microsoft.UI.Xaml.Hosting` | HWND interop |

### Control Replacements (No 1:1 Mapping)

These WPF controls have no direct counterpart and require a different control or third-party package:

| WPF Control | WinUI 3 Replacement | Notes |
|-------------|---------------------|-------|
| `DataGrid` | Community Toolkit `DataGrid` | `CommunityToolkit.WinUI.UI.Controls` — similar API, not identical |
| `Ribbon` | `CommandBar` or `NavigationView` | No Ribbon in WinUI |
| `Menu` / `MenuItem` | `MenuBar` / `MenuBarItem` / `MenuFlyout` | `MenuBar` for classic menu, `MenuFlyout` for context |
| `ContextMenu` | `MenuFlyout` | Assign to `ContextFlyout` property |
| `ToolBar` / `ToolBarTray` | `CommandBar` + `AppBarButton` | |
| `StatusBar` | Custom `Grid`/`StackPanel` or `InfoBar` | No StatusBar control |
| `TabControl` | `TabView` or `NavigationView` (top mode) | `TabView` for closeable tabs |
| `DocumentViewer` | `WebView2` | Render PDFs/XPS inside WebView2 |
| `FlowDocument` | `RichTextBlock` | Partial replacement only |
| `RichTextBox` | `RichEditBox` | Rich text editing |
| `WrapPanel` | Community Toolkit `WrapPanel` | Not in WinUI by default |
| `UniformGrid` | Community Toolkit `UniformGrid` | Not in WinUI by default |
| `DockPanel` | Community Toolkit `DockPanel` | Not in WinUI by default |
| `GroupBox` | `Expander` or custom `HeaderedContentControl` | No GroupBox in WinUI |
| `Label` | `TextBlock` | WPF `Label` is a `ContentControl`; use `TextBlock` + `AccessKey` |
| `TreeView` | `TreeView` (native) | Available natively, but data binding model differs significantly |
| `MessageBox` | `ContentDialog` | Must set `XamlRoot` before `ShowAsync()` |
| `MediaElement` | `MediaPlayerElement` | Different API |
| `AccessText` | Not available | Use `AccessKey` property on target control |

### No Equivalent — Requires Architectural Rework

These WPF features have no WinUI counterpart and require redesign, not find-and-replace:

| WPF Feature | WinUI 3 Replacement Strategy |
|-------------|------------------------------|
| `Style.Triggers` / `DataTrigger` | `VisualStateManager` with `StateTrigger` — see [XAML Migration](./references/xaml-migration.md) |
| `MultiBinding` | `x:Bind` function binding: `{x:Bind local:Converters.Format(VM.A, VM.B), Mode=OneWay}` |
| `RoutedUICommand` / `CommandBinding` | `ICommand` / `[RelayCommand]` from CommunityToolkit.Mvvm. WinUI also has `StandardUICommand` / `XamlUICommand` for platform commands. |
| `AdornerLayer` / `Adorner` | Depends on use case: `TeachingTip`/`InfoBar` (validation), `Popup` (overlays), `PlaceholderText` (watermarks), Canvas overlay (decorations) |
| `Visibility.Hidden` | `Opacity="0"` with `Visibility="Visible"` (preserves layout space) |
| `Window.Resources` / `Window.DataContext` | Move to root `Grid.Resources` / root `Page`/`UserControl` — WinUI `Window` is NOT a DependencyObject |
| Tunneling events (`Preview*`) | Use bubbling equivalents + `Handled` property or `AddHandler(handledEventsToo: true)` |

### Critical API Replacements

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `Dispatcher.Invoke()` | `DispatcherQueue.TryEnqueue()` | Different return type (`bool`), async by default |
| `Dispatcher.CheckAccess()` | `DispatcherQueue.HasThreadAccess` | Property vs method |
| `Application.Current.Dispatcher` | Store `DispatcherQueue` in static field | See [Threading](./references/threading-and-windowing.md) |
| `Window.Current` | Custom `App.Window` static property | Not supported in Windows App SDK |
| `Application.Current.MainWindow` | Custom `App.Window` static property | Must track manually |
| `MessageBox.Show()` | `ContentDialog` | Must set `XamlRoot` |
| `System.Windows.Clipboard` | `Windows.ApplicationModel.DataTransfer.Clipboard` | Different API surface |
| `RoutedUICommand` / `CommandBinding` | `ICommand` / `[RelayCommand]` | Remove `CommandBinding`; bind `ICommand` directly |
| `Properties.Resources.MyString` | `ResourceLoaderInstance.ResourceLoader.GetString("MyString")` | Lazy-init pattern |
| `DynamicResource` | `ThemeResource` | Theme-reactive only |
| `clr-namespace:` | `using:` | XAML namespace prefix |
| `{x:Static props:Resources.Key}` | `x:Uid` or `ResourceLoader.GetString()` | .resx → .resw |
| `DataType="{x:Type m:Foo}"` | `x:DataType="m:Foo"` | `x:Type` not supported |
| `SizeToContent="Height"` | Custom `SizeToContent()` via `AppWindow.Resize()` | See [Windowing](./references/threading-and-windowing.md) |
| `Pack URI (pack://...)` | `ms-appx:///` | Resource URI scheme |
| `Observable` (custom base) | `ObservableObject` + `[ObservableProperty]` | CommunityToolkit.Mvvm |
| `RelayCommand` (custom) | `[RelayCommand]` source generator | CommunityToolkit.Mvvm |
| `JpegBitmapEncoder` | `BitmapEncoder.CreateAsync(JpegEncoderId, stream)` | Async, unified API |
| `encoder.QualityLevel = 85` | `BitmapPropertySet { "ImageQuality", 0.85f }` | int 1-100 → float 0-1 |

### Event Replacements (Mouse → Pointer)

| WPF Event | WinUI 3 Event | Notes |
|-----------|--------------|-------|
| `MouseLeftButtonDown` | `PointerPressed` | Check `IsLeftButtonPressed` on args |
| `MouseLeftButtonUp` | `PointerReleased` | Check pointer properties |
| `MouseRightButtonDown` | `RightTapped` | Or `PointerPressed` with right button check |
| `MouseMove` | `PointerMoved` | `MouseEventArgs` → `PointerRoutedEventArgs` |
| `MouseWheel` | `PointerWheelChanged` | Different event args |
| `MouseEnter` / `MouseLeave` | `PointerEntered` / `PointerExited` | |
| `MouseDoubleClick` | `DoubleTapped` | Different event args |
| `PreviewMouseDown` | `PointerPressed` | No tunneling — use `Handled` or `AddHandler` |
| `PreviewKeyDown` | `KeyDown` | `KeyEventArgs` → `KeyRoutedEventArgs` |

### Property Replacements

| WPF | WinUI 3 | Context |
|-----|---------|---------|
| `Visibility.Hidden` | `Visibility.Collapsed` or `Opacity="0"` | Use `Opacity="0"` to preserve layout |
| `TextWrapping.WrapWithOverflow` | `TextWrapping.Wrap` | WinUI doesn't distinguish |
| `Focusable="True"` | `IsTabStop="True"` | Different property name |
| `ContextMenu=` | `ContextFlyout=` | On any `UIElement` |
| `MediaElement` | `MediaPlayerElement` | Different API |
| `SnapsToDevicePixels` | Not available | WinUI handles pixel snapping internally |

### NuGet Package Migration

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `Microsoft.Xaml.Behaviors.Wpf` | `Microsoft.Xaml.Behaviors.WinUI.Managed` | |
| `WPF-UI` (Lepo) | **Remove** — use native WinUI 3 controls | |
| `CommunityToolkit.Mvvm` | `CommunityToolkit.Mvvm` (same) | |
| `Microsoft.Toolkit.Wpf.*` | `CommunityToolkit.WinUI.*` | |
| (none) | `Microsoft.WindowsAppSDK` | Required |
| (none) | `Microsoft.Windows.SDK.BuildTools` | Required |
| (none) | `WinUIEx` | Optional, window helpers |
| (none) | `CommunityToolkit.WinUI.Converters` | Optional |
| (none) | `CommunityToolkit.WinUI.UI.Controls` | DataGrid, WrapPanel, DockPanel, UniformGrid |

### XAML Syntax Changes

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `xmlns:local="clr-namespace:MyApp"` | `xmlns:local="using:MyApp"` | CLR → using syntax |
| `{DynamicResource Key}` | `{ThemeResource Key}` | Re-evaluates on theme change |
| `{StaticResource Key}` | `{StaticResource Key}` | Same — resolved once at load |
| `{x:Static Type.Member}` | `{x:Bind}` or code-behind | |
| `{x:Type local:MyType}` | Not supported | Use `x:DataType` for DataTemplate |
| `{x:Array}` | Not supported | Create collections in code-behind |
| `<Style.Triggers>` / `<DataTrigger>` | `VisualStateManager` | See [XAML Migration](./references/xaml-migration.md) |
| `{Binding}` in `Setter.Value` | Not supported — use `StaticResource` | |
| `Content="{x:Static p:Resources.Cancel}"` | `x:Uid="Cancel"` with `.Content` in `.resw` | |
| `sys:String` / `sys:Int32` / etc. | `x:String` / `x:Int32` / etc. | XAML intrinsic types |
| `<ui:FluentWindow>` (WPF-UI) | `<Window>` | Native + `ExtendsContentIntoTitleBar` |
| `<ui:NumberBox>` / `<ui:ProgressRing>` (WPF-UI) | Native `<NumberBox>` / `<ProgressRing>` | |
| `BasedOn="{StaticResource {x:Type ui:Button}}"` | `BasedOn="{StaticResource DefaultButtonStyle}"` | Named style keys |
| `IsDefault="True"` / `IsCancel="True"` | `Style="{StaticResource AccentButtonStyle}"` / KeyDown | |
| `<AccessText>` | Not available — use `AccessKey` property | |
| `<behaviors:Interaction.Triggers>` | Code-behind or WinUI behaviors | |
| `Window.Resources` | Root container's `Resources` (e.g. `Grid.Resources`) | Window is not a DependencyObject |

### Binding: {Binding} vs {x:Bind}

Both work in WinUI 3. Prefer `{x:Bind}` for new/migrated code.

| Feature | `{Binding}` | `{x:Bind}` |
|---------|------------|------------|
| Default mode | `OneWay` | **`OneTime`** — add `Mode=OneWay` explicitly! |
| Default source | `DataContext` | Page/UserControl code-behind |
| Compile-time validation | No | Yes |
| Function binding | No | Yes (replaces `MultiBinding`) |
| Performance | Reflection-based | Compiled, no reflection |
| `MultiBinding` support | No (not in WinUI) | Use function binding |

## Detailed Reference Docs

Read only the section relevant to your current task:

- [Namespace and API Mapping](./references/namespace-api-mapping.md) — Full type mapping, NuGet changes, project file, CsWinRT interop
- [XAML Migration Guide](./references/xaml-migration.md) — XAML syntax, WPF-UI removal, markup extensions, styles, resources, data binding
- [Threading and Window Management](./references/threading-and-windowing.md) — Dispatcher, DispatcherQueue, SizeToContent, AppWindow, HWND interop, custom entry point
- [Imaging API Migration](./references/imaging-migration.md) — BitmapEncoder/Decoder, SoftwareBitmap, CodecHelper, async patterns, int→uint
- [PowerToys-Specific Patterns](./references/powertoys-patterns.md) — MVVM migration, ResourceLoader, Lazy init, installer, signing, test adaptation, build pipeline

## Common Pitfalls (from ImageResizer migration)

| Pitfall | Solution |
|---------|----------|
| `ContentDialog` throws "does not have a XamlRoot" | Set `dialog.XamlRoot = this.Content.XamlRoot` before `ShowAsync()` |
| `FilePicker` throws error in desktop app | Call `WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd)` |
| `Window.Dispatcher` returns null | Use `Window.DispatcherQueue` instead |
| Resources on `Window` element not found | Move resources to root layout container (`Grid.Resources`) |
| `VisualStateManager` on `Window` fails | Use `UserControl` or `Page` inside the Window |
| Satellite assembly installer errors (`WIX0103`) | Remove `.resources.dll` refs from `Resources.wxs`; WinUI 3 uses `.pri` |
| Phantom `.exe`/`.deps.json` in root output dir | Avoid Exe→WinExe `ProjectReference`; use Library project |
| `ResourceLoader` crash at static init | Wrap in `Lazy<T>` or null-coalescing property — see [Lazy Init](./references/powertoys-patterns.md#lazy-initialization-for-resource-dependent-statics) |
| `SizeToContent` not available | Implement manual content measurement + `AppWindow.Resize()` with DPI scaling |
| `x:Bind` default mode is `OneTime` | Explicitly set `Mode=OneWay` or `Mode=TwoWay` |
| `DynamicResource` / `x:Static` not compiling | Replace with `ThemeResource` / `ResourceLoader` or `x:Uid` |
| `IValueConverter.Convert` signature mismatch | Last param: `CultureInfo` → `string` (language tag) |
| Test project can't resolve WPF types | Add `<UseWPF>true</UseWPF>` temporarily; remove after imaging migration |
| Pixel dimension type mismatch (`int` vs `uint`) | WinRT uses `uint` for pixel sizes — add `u` suffix in test assertions |
| `$(SolutionDir)` empty in standalone project build | Use `$(MSBuildThisFileDirectory)` with relative paths instead |
| JPEG quality value wrong after migration | WPF: int 1-100; WinRT: float 0.0-1.0 |
| MSIX packaging fails in PreBuildEvent | Move to PostBuildEvent; artifacts not ready at PreBuild time |
| RC file icon path with forward slashes | Use double-backslash escaping: `..\\ui\\Assets\\icon.ico` |
| `COMException: ClassFactory cannot supply requested class` at startup | Missing `<WindowsPackageType>None</WindowsPackageType>` and/or `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` in csproj. Without these, the app tries to locate the Windows App SDK framework package (not installed) instead of using bundled runtime DLLs. **Both properties are mandatory for every WinUI 3 module in PowerToys.** |
| `CombinedGeometry` not available in WinUI 3 | WinUI 3 `UIElement.Clip` only accepts `RectangleGeometry`. For overlay hole effects (exclude region), use a `Path` element with `GeometryGroup FillRule="EvenOdd"` containing two `RectangleGeometry` children — the EvenOdd rule creates a transparent hole where geometries overlap. |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Build fails after namespace rename | Check for lingering `System.Windows` usings; some types have no direct equivalent |
| Missing `PresentationCore.dll` at runtime | Ensure ALL imaging code uses `Windows.Graphics.Imaging`, not `System.Windows.Media.Imaging` |
| `DataContext` not working on Window | WinUI 3 `Window` is not a `DependencyObject`; use a root `Page` or `UserControl` |
| XAML designer not available | WinUI 3 does not support XAML Designer; use Hot Reload instead |
| NuGet restore failures | Run `build-essentials.cmd` after adding `Microsoft.WindowsAppSDK` package |
| `Parallel.ForEach` compilation error | Migrate to `Parallel.ForEachAsync` for async imaging operations |
| Signing check fails on leaked artifacts | Run `generateAllFileComponents.ps1`; verify only `WinUI3Apps\\` paths in signing config |
| `COMException` / `ClassFactory` error at app launch | Ensure csproj has `<WindowsPackageType>None</WindowsPackageType>` and `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`. These are required for all unpackaged WinUI 3 apps in PowerToys — without them the WinUI 3 COM runtime cannot be found. |
