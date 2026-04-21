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

### Recommended Order

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

### Key Principles

- **Do NOT overwrite `App.xaml` / `App.xaml.cs`** — WinUI 3 has different application lifecycle boilerplate. Merge your resources and initialization code into the generated WinUI 3 App class.
- **Do NOT create Exe→WinExe `ProjectReference`** — Extract shared code to a Library project. This causes phantom build artifacts.
- **Use `Lazy<T>` for resource-dependent statics** — `ResourceLoader` is not available at class-load time in all contexts.

## Quick Reference Tables

### Namespace Mapping

| WPF | WinUI 3 |
|-----|---------|
| `System.Windows` | `Microsoft.UI.Xaml` |
| `System.Windows.Controls` | `Microsoft.UI.Xaml.Controls` |
| `System.Windows.Media` | `Microsoft.UI.Xaml.Media` |
| `System.Windows.Media.Imaging` | `Microsoft.UI.Xaml.Media.Imaging` (UI) / `Windows.Graphics.Imaging` (processing) |
| `System.Windows.Input` | `Microsoft.UI.Xaml.Input` |
| `System.Windows.Data` | `Microsoft.UI.Xaml.Data` |
| `System.Windows.Threading` | `Microsoft.UI.Dispatching` |
| `System.Windows.Interop` | `WinRT.Interop` |

### Critical API Replacements

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `Dispatcher.Invoke()` | `DispatcherQueue.TryEnqueue()` | Different return type (`bool`) |
| `Dispatcher.CheckAccess()` | `DispatcherQueue.HasThreadAccess` | Property vs method |
| `Application.Current.Dispatcher` | Store `DispatcherQueue` in static field | See [Threading](./references/threading-and-windowing.md) |
| `MessageBox.Show()` | `ContentDialog` | Must set `XamlRoot` |
| `DynamicResource` | `ThemeResource` | Theme-reactive only |
| `clr-namespace:` | `using:` | XAML namespace prefix |
| `{x:Static props:Resources.Key}` | `x:Uid` or `ResourceLoader.GetString()` | .resx → .resw |
| `DataType="{x:Type m:Foo}"` | Remove or use code-behind | `x:Type` not supported |
| `Properties.Resources.MyString` | `ResourceLoaderInstance.ResourceLoader.GetString("MyString")` | Lazy-init pattern |
| `Application.Current.MainWindow` | Custom `App.Window` static property | Must track manually |
| `SizeToContent="Height"` | Custom `SizeToContent()` via `AppWindow.Resize()` | See [Windowing](./references/threading-and-windowing.md) |
| `MouseLeftButtonDown` | `PointerPressed` | Mouse → Pointer events |
| `Pack URI (pack://...)` | `ms-appx:///` | Resource URI scheme |
| `Observable` (custom base) | `ObservableObject` + `[ObservableProperty]` | CommunityToolkit.Mvvm |
| `RelayCommand` (custom) | `[RelayCommand]` source generator | CommunityToolkit.Mvvm |
| `JpegBitmapEncoder` | `BitmapEncoder.CreateAsync(JpegEncoderId, stream)` | Async, unified API |
| `encoder.QualityLevel = 85` | `BitmapPropertySet { "ImageQuality", 0.85f }` | int 1-100 → float 0-1 |

### NuGet Package Migration

| WPF | WinUI 3 |
|-----|---------|
| `Microsoft.Xaml.Behaviors.Wpf` | `Microsoft.Xaml.Behaviors.WinUI.Managed` |
| `WPF-UI` (Lepo) | Remove — use native WinUI 3 controls |
| `CommunityToolkit.Mvvm` | `CommunityToolkit.Mvvm` (same) |
| `Microsoft.Toolkit.Wpf.*` | `CommunityToolkit.WinUI.*` |
| (none) | `Microsoft.WindowsAppSDK` |
| (none) | `Microsoft.Windows.SDK.BuildTools` |
| (none) | `WinUIEx` (optional, for window helpers) |
| (none) | `CommunityToolkit.WinUI.Converters` |

### XAML Syntax Changes

| WPF | WinUI 3 |
|-----|---------|
| `xmlns:local="clr-namespace:MyApp"` | `xmlns:local="using:MyApp"` |
| `{DynamicResource Key}` | `{ThemeResource Key}` |
| `{x:Static Type.Member}` | `{x:Bind}` or code-behind |
| `{x:Type local:MyType}` | Not supported |
| `<Style.Triggers>` / `<DataTrigger>` | `VisualStateManager` |
| `{Binding}` in `Setter.Value` | Not supported — use `StaticResource` |
| `Content="{x:Static p:Resources.Cancel}"` | `x:Uid="Cancel"` with `.Content` in `.resw` |
| `<ui:FluentWindow>` / `<ui:Button>` (WPF-UI) | Native `<Window>` / `<Button>` |
| `<ui:NumberBox>` / `<ui:ProgressRing>` (WPF-UI) | Native `<NumberBox>` / `<ProgressRing>` |
| `BasedOn="{StaticResource {x:Type ui:Button}}"` | `BasedOn="{StaticResource DefaultButtonStyle}"` |
| `IsDefault="True"` / `IsCancel="True"` | `Style="{StaticResource AccentButtonStyle}"` / handle via KeyDown |
| `<AccessText>` | Not available — use `AccessKey` property |
| `<behaviors:Interaction.Triggers>` | Migrate to code-behind or WinUI behaviors |

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
