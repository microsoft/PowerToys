# Namespace and API Mapping Reference

Complete reference for mapping WPF types to WinUI 3 equivalents, based on the ImageResizer migration.

## Root Namespace Mapping

| WPF Namespace | WinUI 3 Namespace |
|---------------|-------------------|
| `System.Windows` | `Microsoft.UI.Xaml` |
| `System.Windows.Automation` | `Microsoft.UI.Xaml.Automation` |
| `System.Windows.Automation.Peers` | `Microsoft.UI.Xaml.Automation.Peers` |
| `System.Windows.Controls` | `Microsoft.UI.Xaml.Controls` |
| `System.Windows.Controls.Primitives` | `Microsoft.UI.Xaml.Controls.Primitives` |
| `System.Windows.Data` | `Microsoft.UI.Xaml.Data` |
| `System.Windows.Documents` | `Microsoft.UI.Xaml.Documents` |
| `System.Windows.Input` | `Microsoft.UI.Xaml.Input` |
| `System.Windows.Markup` | `Microsoft.UI.Xaml.Markup` |
| `System.Windows.Media` | `Microsoft.UI.Xaml.Media` |
| `System.Windows.Media.Animation` | `Microsoft.UI.Xaml.Media.Animation` |
| `System.Windows.Media.Imaging` | `Microsoft.UI.Xaml.Media.Imaging` |
| `System.Windows.Navigation` | `Microsoft.UI.Xaml.Navigation` |
| `System.Windows.Shapes` | `Microsoft.UI.Xaml.Shapes` |
| `System.Windows.Threading` | `Microsoft.UI.Dispatching` |
| `System.Windows.Interop` | `WinRT.Interop` |

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
- Add `WindowsPackageType=None` for unpackaged desktop apps
- Add `SelfContained=true` + `WindowsAppSDKSelfContained=true`
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
