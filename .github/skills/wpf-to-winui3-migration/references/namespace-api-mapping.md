# Namespace and API Mapping Reference

Complete reference for mapping WPF types to WinUI 3 equivalents, based on the ImageResizer migration.

## Root Namespace Mapping

| WPF Namespace | WinUI 3 Namespace | Notes |
|---------------|-------------------|-------|
| `System.Windows` | `Microsoft.UI.Xaml` | Root namespace |
| `System.Windows.Automation` | `Microsoft.UI.Xaml.Automation` | Accessibility / UI Automation |
| `System.Windows.Automation.Peers` | `Microsoft.UI.Xaml.Automation.Peers` | |
| `System.Windows.Controls` | `Microsoft.UI.Xaml.Controls` | Core controls |
| `System.Windows.Controls.Primitives` | `Microsoft.UI.Xaml.Controls.Primitives` | Low-level primitives |
| `System.Windows.Data` | `Microsoft.UI.Xaml.Data` | Binding, IValueConverter |
| `System.Windows.Documents` | `Microsoft.UI.Xaml.Documents` | Limited — RichTextBlock + Paragraph only |
| `System.Windows.Input` | `Microsoft.UI.Xaml.Input` | Pointer, keyboard, focus |
| `System.Windows.Markup` | `Microsoft.UI.Xaml.Markup` | XAML parsing, markup extensions |
| `System.Windows.Media` | `Microsoft.UI.Xaml.Media` | Brushes, transforms |
| `System.Windows.Media.Animation` | `Microsoft.UI.Xaml.Media.Animation` | Storyboard, animations |
| `System.Windows.Media.Imaging` | `Microsoft.UI.Xaml.Media.Imaging` | UI display only — use `Windows.Graphics.Imaging` for processing |
| `System.Windows.Media.Media3D` | **No equivalent** | Use Win2D or Composition APIs |
| `System.Windows.Navigation` | `Microsoft.UI.Xaml.Navigation` | For Frame navigation events; no `NavigationService` |
| `System.Windows.Shapes` | `Microsoft.UI.Xaml.Shapes` | Rectangle, Ellipse, Path |
| `System.Windows.Threading` | `Microsoft.UI.Dispatching` | Dispatcher → DispatcherQueue |
| `System.Windows.Interop` | `WinRT.Interop` | HWND interop |
| `System.Windows.Interop` | `Microsoft.UI.Xaml.Hosting` | XAML Islands |

## Core Type Mapping

| WPF Type | WinUI 3 Type |
|----------|-------------|
| `System.Windows.Application` | `Microsoft.UI.Xaml.Application` |
| `System.Windows.Window` | `Microsoft.UI.Xaml.Window` (NOT a DependencyObject) |
| `System.Windows.DependencyObject` | `Microsoft.UI.Xaml.DependencyObject` |
| `System.Windows.DependencyProperty` | `Microsoft.UI.Xaml.DependencyProperty` |
| `System.Windows.FrameworkElement` | `Microsoft.UI.Xaml.FrameworkElement` |
| `System.Windows.UIElement` | `Microsoft.UI.Xaml.UIElement` |
| `System.Windows.Visibility` | `Microsoft.UI.Xaml.Visibility` |
| `System.Windows.Thickness` | `Microsoft.UI.Xaml.Thickness` |
| `System.Windows.CornerRadius` | `Microsoft.UI.Xaml.CornerRadius` |
| `System.Windows.Media.Color` | `Windows.UI.Color` (note: `Windows.UI`, not `Microsoft.UI`) |
| `System.Windows.Media.Colors` | `Microsoft.UI.Colors` |

## Controls Mapping

### Direct Mapping (namespace-only change)

These controls exist in both frameworks with the same name — change `System.Windows.Controls` to `Microsoft.UI.Xaml.Controls`:

`Button`, `TextBox`, `TextBlock`, `ComboBox`, `CheckBox`, `ListBox`, `ListView`, `Image`, `StackPanel`, `Grid`, `Border`, `ScrollViewer`, `ContentControl`, `UserControl`, `Page`, `Frame`, `Slider`, `ProgressBar`, `ToolTip`, `RadioButton`, `ToggleButton`

### Controls With Different Names or Behavior

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `MessageBox` | `ContentDialog` | Must set `XamlRoot` before `ShowAsync()` |
| `ContextMenu` | `MenuFlyout` | Different API surface |
| `TabControl` | `TabView` | Different API |
| `Menu` | `MenuBar` | Different API |
| `StatusBar` | Custom `StackPanel` layout | No built-in equivalent |
| `AccessText` | Not available | Use `AccessKey` property on target control |

### WPF-UI (Lepo) to Native WinUI 3

ImageResizer used the `WPF-UI` library (Lepo) for Fluent styling. These must be replaced with native WinUI 3 equivalents:

| WPF-UI (Lepo) | WinUI 3 Native | Notes |
|----------------|---------------|-------|
| `<ui:FluentWindow>` | `<Window>` | Native window + `ExtendsContentIntoTitleBar` |
| `<ui:Button>` | `<Button>` | Native button |
| `<ui:NumberBox>` | `<NumberBox>` | Built into WinUI 3 |
| `<ui:ProgressRing>` | `<ProgressRing>` | Built into WinUI 3 |
| `<ui:SymbolIcon>` | `<SymbolIcon>` or `<FontIcon>` | Built into WinUI 3 |
| `<ui:InfoBar>` | `<InfoBar>` | Built into WinUI 3 |
| `<ui:TitleBar>` | Custom title bar via `SetTitleBar()` | Use `ExtendsContentIntoTitleBar` |
| `<ui:ThemesDictionary>` | `<XamlControlsResources>` | In merged dictionaries |
| `<ui:ControlsDictionary>` | Remove | Not needed — WinUI 3 has its own control styles |
| `BasedOn="{StaticResource {x:Type ui:Button}}"` | `BasedOn="{StaticResource DefaultButtonStyle}"` | Named style keys |

## Input Event Mapping

| WPF Event | WinUI 3 Event | Notes |
|-----------|--------------|-------|
| `MouseLeftButtonDown` | `PointerPressed` | Check `IsLeftButtonPressed` on args |
| `MouseLeftButtonUp` | `PointerReleased` | Check pointer properties |
| `MouseRightButtonDown` | `RightTapped` | Or `PointerPressed` with right button check |
| `MouseMove` | `PointerMoved` | Uses `PointerRoutedEventArgs` |
| `MouseWheel` | `PointerWheelChanged` | Different event args |
| `MouseEnter` | `PointerEntered` | |
| `MouseLeave` | `PointerExited` | |
| `MouseDoubleClick` | `DoubleTapped` | Different event args |
| `KeyDown` | `KeyDown` | Same name, args type: `KeyRoutedEventArgs` |
| `PreviewKeyDown` | No direct equivalent | Use `KeyDown` with handled pattern |

## IValueConverter Signature Change

| WPF | WinUI 3 |
|-----|---------|
| `Convert(object value, Type targetType, object parameter, CultureInfo culture)` | `Convert(object value, Type targetType, object parameter, string language)` |
| `ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)` | `ConvertBack(object value, Type targetType, object parameter, string language)` |

Last parameter changes from `CultureInfo` to `string` (BCP-47 language tag). All converter classes must be updated.

## Types That Moved to Different Hierarchies

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `System.Windows.Threading.Dispatcher` | `Microsoft.UI.Dispatching.DispatcherQueue` | Completely different API |
| `System.Windows.Threading.DispatcherPriority` | `Microsoft.UI.Dispatching.DispatcherQueuePriority` | Only 3 levels: High/Normal/Low |
| `System.Windows.Interop.HwndSource` | `WinRT.Interop.WindowNative` | For HWND interop |
| `System.Windows.Interop.WindowInteropHelper` | `WinRT.Interop.WindowNative.GetWindowHandle()` | |
| `System.Windows.SystemColors` | Resource keys via `ThemeResource` | No direct static class |
| `System.Windows.SystemParameters` | Win32 API or `DisplayInformation` | No direct equivalent |
| `System.Windows.Clipboard` | `Windows.ApplicationModel.DataTransfer.Clipboard` | Different API surface |
| `System.Windows.Input.RoutedUICommand` | `Microsoft.UI.Xaml.Input.StandardUICommand` / `XamlUICommand` | Or use `ICommand` / `[RelayCommand]` |
| `System.Windows.Input.CommandBinding` | **Remove** | Bind `ICommand` directly in XAML |

## Controls That Need Translation (No 1:1 Mapping)

These controls exist in WPF but require a different control, third-party library, or Community Toolkit package in WinUI 3:

| WPF Control | WinUI 3 Replacement | Package / Notes |
|-------------|---------------------|-----------------|
| `DataGrid` | `DataGrid` | `CommunityToolkit.WinUI.UI.Controls` — similar API, not identical |
| `Ribbon` | `CommandBar` or `NavigationView` | No Ribbon in WinUI |
| `Menu` / `MenuItem` | `MenuBar` / `MenuBarItem` / `MenuFlyoutItem` | `MenuBar` for classic menu, `MenuFlyout` for context |
| `ContextMenu` | `MenuFlyout` | Assign to `ContextFlyout` property |
| `ToolBar` / `ToolBarTray` | `CommandBar` + `AppBarButton` | |
| `StatusBar` | Custom `Grid`/`StackPanel` or `InfoBar` | No StatusBar control |
| `TabControl` | `TabView` or `NavigationView` (top mode) | `TabView` for closeable tabs |
| `DocumentViewer` | `WebView2` | `Microsoft.Web.WebView2` — render PDFs/XPS |
| `FlowDocument` | `RichTextBlock` | Partial replacement only |
| `RichTextBox` | `RichEditBox` | Rich text editing |
| `WrapPanel` | `WrapPanel` | `CommunityToolkit.WinUI.UI.Controls` |
| `UniformGrid` | `UniformGrid` | `CommunityToolkit.WinUI.UI.Controls` |
| `DockPanel` | `DockPanel` | `CommunityToolkit.WinUI.UI.Controls` |
| `GroupBox` | `Expander` or custom `HeaderedContentControl` | No GroupBox in WinUI |
| `Label` | `TextBlock` | WPF `Label` is a `ContentControl`; use `TextBlock` + `AccessKey` |
| `TreeView` | `TreeView` (native) | Available natively, but data binding model differs significantly |
| `MediaElement` | `MediaPlayerElement` | Different API |

## NuGet Package Migration

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| Built into .NET (no NuGet needed) | `Microsoft.WindowsAppSDK` | Required |
| `PresentationCore` / `PresentationFramework` | `Microsoft.WinUI` (transitive) | |
| `Microsoft.Xaml.Behaviors.Wpf` | `Microsoft.Xaml.Behaviors.WinUI.Managed` | |
| `WPF-UI` (Lepo) | **Remove** — use native WinUI 3 controls | |
| `CommunityToolkit.Mvvm` | `CommunityToolkit.Mvvm` (same) | |
| `Microsoft.Toolkit.Wpf.*` | `CommunityToolkit.WinUI.*` | |
| (none) | `Microsoft.Windows.SDK.BuildTools` | Required |
| (none) | `WinUIEx` | Optional, window helpers |
| (none) | `CommunityToolkit.WinUI.Converters` | Optional |
| (none) | `CommunityToolkit.WinUI.Extensions` | Optional |
| (none) | `Microsoft.Web.WebView2` | If using WebView |

## Project File Changes

### WPF .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ApplicationManifest>ImageResizerUI.dev.manifest</ApplicationManifest>
    <ApplicationIcon>Resources\ImageResizer.ico</ApplicationIcon>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
</Project>
```

### WinUI 3 .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <SelfContained>true</SelfContained>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <WindowsPackageType>None</WindowsPackageType>
    <EnablePreviewMsixTooling>true</EnablePreviewMsixTooling>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\ImageResizer\ImageResizer.ico</ApplicationIcon>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <DefineConstants>DISABLE_XAML_GENERATED_MAIN,TRACE</DefineConstants>
    <ProjectPriFileName>PowerToys.ModuleName.pri</ProjectPriFileName>
  </PropertyGroup>
</Project>
```

Key changes:
- `UseWPF` → `UseWinUI`
- TFM: `net8.0-windows` → `net8.0-windows10.0.19041.0`
- **CRITICAL: Add `WindowsPackageType=None`** — marks the app as unpackaged (no MSIX). Without this, the build produces an MSIX-style package that won't run as a standalone PowerToys module.
- **CRITICAL: Add `WindowsAppSDKSelfContained=true`** — bundles the Windows App SDK runtime DLLs (e.g. `Microsoft.UI.Xaml.dll`) into the output directory. Without this, the app throws `COMException: ClassFactory cannot supply requested class` at startup because the WinUI 3 COM classes cannot be found.
- Add `SelfContained=true` (usually via `Common.SelfContained.props`)
- Add `DISABLE_XAML_GENERATED_MAIN` if using custom `Program.cs` entry point
- Set `ProjectPriFileName` to match your module's assembly name
- Move icon from `Resources/` to `Assets/<Module>/`

### XAML ApplicationDefinition Setup

WinUI 3 requires explicit `ApplicationDefinition` declaration:

```xml
<ItemGroup>
  <Page Remove="ImageResizerXAML\App.xaml" />
</ItemGroup>
<ItemGroup>
  <ApplicationDefinition Include="ImageResizerXAML\App.xaml" />
</ItemGroup>
```

### CsWinRT Interop (for GPO and native references)

If the module references native C++ projects (like `GPOWrapper`):

```xml
<PropertyGroup>
  <CsWinRTIncludes>PowerToys.GPOWrapper</CsWinRTIncludes>
  <CsWinRTGeneratedFilesDir>$(OutDir)</CsWinRTGeneratedFilesDir>
</PropertyGroup>
```

Change `GPOWrapperProjection.csproj` reference to direct `GPOWrapper.vcxproj` reference.

### InternalsVisibleTo Migration

Move from code file to `.csproj`:

```csharp
// DELETE: Properties/InternalsVisibleTo.cs
// [assembly: InternalsVisibleTo("ImageResizer.Test")]
```

```xml
<!-- ADD to .csproj: -->
<ItemGroup>
  <InternalsVisibleTo Include="ImageResizer.Test" />
</ItemGroup>
```

### Items to Remove from .csproj

```xml
<!-- DELETE: WPF resource embedding -->
<EmbeddedResource Update="Properties\Resources.resx">...</EmbeddedResource>
<Resource Include="Resources\ImageResizer.ico" />
<Compile Update="Properties\Resources.Designer.cs">...</Compile>
<FrameworkReference Include="Microsoft.WindowsDesktop.App.WPF" /> <!-- from CLI project -->
```
