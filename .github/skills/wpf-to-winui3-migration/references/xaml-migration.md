# XAML Migration Guide

Detailed reference for migrating XAML from WPF to WinUI 3, based on the ImageResizer migration.

## XML Namespace Declaration Changes

### Before (WPF)

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:MyApp"
        xmlns:m="clr-namespace:ImageResizer.Models"
        xmlns:p="clr-namespace:ImageResizer.Properties"
        xmlns:sys="clr-namespace:System;assembly=mscorlib"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        x:Class="MyApp.MainWindow">
```

### After (WinUI 3)

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="using:MyApp"
        xmlns:m="using:ImageResizer.Models"
        xmlns:converters="using:ImageResizer.Converters"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        x:Class="MyApp.MainWindow">
```

### Key Changes

| WPF Syntax | WinUI 3 Syntax | Notes |
|------------|---------------|-------|
| `clr-namespace:Foo` | `using:Foo` | CLR namespace mapping |
| `clr-namespace:Foo;assembly=Bar` | `using:Foo` | Assembly qualification not needed |
| `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"` | **Remove entirely** | WPF-UI namespace no longer needed |
| `xmlns:p="clr-namespace:...Properties"` | **Remove** | No more `.resx` string bindings |
| `sys:String` (from mscorlib) | `x:String` | XAML intrinsic types |
| `sys:Int32` | `x:Int32` | XAML intrinsic types |
| `sys:Boolean` | `x:Boolean` | XAML intrinsic types |
| `sys:Double` | `x:Double` | XAML intrinsic types |

## Unsupported Markup Extensions

| WPF Markup Extension | WinUI 3 Alternative |
|----------------------|---------------------|
| `{DynamicResource Key}` | `{ThemeResource Key}` (theme-reactive) or `{StaticResource Key}` |
| `{x:Static Type.Member}` | `{x:Bind}` to a static property, or code-behind |
| `{x:Type local:MyType}` | Not supported; use code-behind |
| `{x:Array}` | Not supported; create collections in code-behind |
| `{x:Code}` | Not supported |

### DynamicResource → ThemeResource

```xml
<!-- WPF -->
<TextBlock Foreground="{DynamicResource MyBrush}" />

<!-- WinUI 3 -->
<TextBlock Foreground="{ThemeResource MyBrush}" />
```

`ThemeResource` automatically updates when the app theme changes (Light/Dark/HighContrast). For truly dynamic non-theme resources, set values in code-behind or use data binding.

### x:Static Resource Strings → x:Uid

This is the most pervasive XAML change. WPF used `{x:Static}` to bind to strongly-typed `.resx` resource strings. WinUI 3 uses `x:Uid` with `.resw` files.

**WPF:**
```xml
<Button Content="{x:Static p:Resources.Cancel}" />
<TextBlock Text="{x:Static p:Resources.Input_Header}" />
```

**WinUI 3:**
```xml
<Button x:Uid="Cancel" />
<TextBlock x:Uid="Input_Header" />
```

In `Strings/en-us/Resources.resw`:
```xml
<data name="Cancel.Content" xml:space="preserve">
    <value>Cancel</value>
</data>
<data name="Input_Header.Text" xml:space="preserve">
    <value>Select a size</value>
</data>
```

The `x:Uid` suffix (`.Content`, `.Text`, `.Header`, `.PlaceholderText`, etc.) matches the target property name.

### DataType with x:Type → Remove

**WPF:**
```xml
<DataTemplate DataType="{x:Type m:ResizeSize}">
```

**WinUI 3:**
```xml
<DataTemplate x:DataType="m:ResizeSize">
```

## WPF-UI (Lepo) Controls Removal

If the module uses the `WPF-UI` library, replace all Lepo controls with native WinUI 3 equivalents.

### Window

```xml
<!-- WPF (WPF-UI) -->
<ui:FluentWindow
    ExtendsContentIntoTitleBar="True"
    WindowStartupLocation="CenterScreen">
    <ui:TitleBar Title="Image Resizer" />
    ...
</ui:FluentWindow>

<!-- WinUI 3 (native) -->
<Window>
    <!-- Title bar managed via code-behind: this.ExtendsContentIntoTitleBar = true; -->
    ...
</Window>
```

#### Recommended: Use WindowEx from WinUIEx

> **Tip:** Prefer `WinUIEx.WindowEx` over bare `Window`. It restores many WPF-like window properties directly in XAML, avoiding boilerplate code-behind for common windowing tasks.

```xml
<!-- WinUI 3 with WindowEx (preferred in PowerToys) -->
<winuiex:WindowEx
    xmlns:winuiex="using:WinUIEx"
    x:Class="MyApp.MainWindow"
    MinWidth="480"
    MinHeight="320"
    IsShownInSwitchers="True"
    IsTitleBarVisible="True">
    <Window.SystemBackdrop>
        <MicaBackdrop />
    </Window.SystemBackdrop>
    <Grid>
        ...
    </Grid>
</winuiex:WindowEx>
```

Properties available on `WindowEx` that mirror WPF `Window`:

| WPF Window Property | WindowEx Property | Notes |
|---------------------|-------------------|-------|
| `MinWidth` / `MinHeight` | `MinWidth` / `MinHeight` | Set directly in XAML |
| `Width` / `Height` | `Width` / `Height` | Initial window size |
| `WindowState` | `WindowState` | Minimized, Maximized, Normal |
| `Title` | `Title` or `x:Uid` | Window title |
| `Icon` | Use `TitleBar.IconSource` | Via WinUI TitleBar control |
| `ShowInTaskbar` | `IsShownInSwitchers` | Alt-Tab visibility |
| `TopMost` | `IsAlwaysOnTop` | Always-on-top window |

NuGet: `WinUIEx` — already referenced by most PowerToys modules.

#### Recommended: Page-in-Window Architecture

> **Tip:** WinUI 3 `Window` is NOT a `FrameworkElement` — it does not support `Resources`, `DataContext`, `x:Bind`, or `VisualStateManager` directly. Place a `Page` as the Window's root content to regain these WPF-like capabilities.

```xml
<!-- WinUI 3 — Window contains a Page for full FrameworkElement support -->
<winuiex:WindowEx x:Class="MyApp.MainWindow"
    xmlns:winuiex="using:WinUIEx"
    xmlns:views="using:MyApp.Views">
    <views:MainPage x:Name="mainPage" />
</winuiex:WindowEx>
```

```xml
<!-- MainPage.xaml — has full FrameworkElement capabilities -->
<Page x:Class="MyApp.Views.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <Page.Resources>
        <!-- Resources work here (unlike on Window) -->
        <SolidColorBrush x:Key="MyBrush" Color="Red"/>
    </Page.Resources>
    <Grid>
        <VisualStateManager.VisualStateGroups>
            <!-- VisualStateManager works here (unlike on Window) -->
        </VisualStateManager.VisualStateGroups>
        ...
    </Grid>
</Page>
```

This is the standard pattern in PowerToys (e.g., FileLocksmith, EnvironmentVariables).

### App.xaml Resources

```xml
<!-- WPF (WPF-UI) -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Dark" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>

<!-- WinUI 3 (native) -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### CommunityToolkit.WinUI — WPF Replacement Controls

> **Tip:** The `CommunityToolkit.WinUI` package provides many controls and helpers familiar to WPF developers that are missing from WinUI 3 out of the box. Before writing custom replacements, check whether CommunityToolkit already provides what you need.

Key packages (XAML namespace is `using:CommunityToolkit.WinUI.Controls` for the `Controls.*` family):
- **`CommunityToolkit.WinUI.Controls.Primitives`** — `WrapPanel`, `UniformGrid`, `DockPanel`, `ConstrainedBox`, `HeaderedContentControl`
- **`CommunityToolkit.WinUI.Controls.SettingsControls`** — `SettingsCard`, `SettingsExpander`
- **`CommunityToolkit.WinUI.Controls.Sizers`** — `GridSplitter`, `PropertySizer`, `ContentSizer`
- **`CommunityToolkit.WinUI.UI.Controls.DataGrid`** — legacy v7 `DataGrid` (no longer maintained); prefer [`WinUI.TableView`](https://github.com/w-ahmad/WinUI.TableView) for new work
- **`CommunityToolkit.WinUI.Converters`** — Common value converters (`BoolToVisibilityConverter`, `StringFormatConverter`, etc.)
- **`CommunityToolkit.WinUI.Behaviors`** — XAML behaviors for animations and interactions
- **`CommunityToolkit.WinUI.Extensions`** — Extension methods for WinUI types

### Common Control Replacements

```xml
<!-- WPF-UI NumberBox -->
<ui:NumberBox Value="{Binding Width}" />
<!-- WinUI 3 -->
<NumberBox Value="{x:Bind ViewModel.Width, Mode=TwoWay}" />

<!-- WPF-UI InfoBar -->
<ui:InfoBar Title="Warning" Message="..." IsOpen="True" Severity="Warning" />
<!-- WinUI 3 -->
<InfoBar Title="Warning" Message="..." IsOpen="True" Severity="Warning" />

<!-- WPF-UI ProgressRing -->
<ui:ProgressRing IsIndeterminate="True" />
<!-- WinUI 3 -->
<ProgressRing IsActive="True" />

<!-- WPF-UI SymbolIcon -->
<ui:SymbolIcon Symbol="Add" />
<!-- WinUI 3 -->
<SymbolIcon Symbol="Add" />
```

### Button Patterns

```xml
<!-- WPF -->
<Button IsDefault="True" Content="OK" />
<Button IsCancel="True" Content="Cancel" />

<!-- WinUI 3 (no IsDefault/IsCancel) -->
<Button Style="{StaticResource AccentButtonStyle}" Content="OK" />
<Button Content="Cancel" />
<!-- Handle Enter/Escape keys in code-behind if needed -->
```

## No-Equivalent Patterns (Requires Architectural Rework)

These WPF features demand design changes, not find-and-replace. Read this section BEFORE attempting to migrate any file that uses these patterns.

### MultiBinding → x:Bind Function Binding

WinUI does not support `MultiBinding`. Replace with `x:Bind` function binding (most direct replacement), a computed ViewModel property, or multiple simple bindings.

**WPF:**
```xml
<TextBlock>
    <TextBlock.Text>
        <MultiBinding StringFormat="{}{0} {1}">
            <Binding Path="FirstName" />
            <Binding Path="LastName" />
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>
```

**WinUI 3:**
```xml
<TextBlock Text="{x:Bind local:Converters.FormatFullName(ViewModel.FirstName, ViewModel.LastName), Mode=OneWay}" />
```

```csharp
public static class Converters
{
    public static string FormatFullName(string first, string last) => $"{first} {last}";
}
```

### Adorners → Context-Dependent Replacements

WPF's `AdornerLayer` has no WinUI equivalent. Choose replacement by use case:

| Adorner Use Case | WinUI 3 Replacement |
|------------------|---------------------|
| Validation indicators | `TeachingTip`, `InfoBar`, or InputValidation templates |
| Resize handles | `Popup` positioned relative to target |
| Drag preview | `DragItemsStarting` event with custom DragUI |
| Overlay decorations | Canvas overlay or Popup layer |
| Watermark / Placeholder | `TextBox.PlaceholderText` (built-in) |

### RoutedUICommand → ICommand / RelayCommand

WinUI does not support routed commands or `CommandBinding`. Replace with standard `ICommand` pattern:

```csharp
// CommunityToolkit.Mvvm
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { /* save logic */ }
private bool CanSave() => IsDirty;
```

WinUI 3 also provides `StandardUICommand` and `XamlUICommand` for pre-defined platform commands (Cut, Copy, Paste, Delete) with built-in icons and keyboard accelerators.

### Tunneling / Preview Events

WinUI has no tunneling event model. `PreviewMouseDown`, `PreviewKeyDown`, etc. do not exist.

- Replace with the bubbling equivalent (`PointerPressed`, `KeyDown`)
- If you relied on tunneling to intercept events before children, restructure using the `Handled` property
- For must-handle scenarios, use `AddHandler` with `handledEventsToo: true`:
```csharp
myElement.AddHandler(UIElement.PointerPressedEvent,
    new PointerEventHandler(OnPointerPressed), handledEventsToo: true);
```

## Style and Template Changes

### Implicit Styles — Always Use BasedOn

> **Warning:** In WinUI 3, always use `BasedOn` when overriding default control styles. Without it, your style **replaces the entire default style** rather than extending it.

```xml
<!-- WRONG — replaces entire default style, control may lose all visual appearance -->
<Style TargetType="Button">
    <Setter Property="Background" Value="Red" />
</Style>

<!-- CORRECT — extends the default style -->
<Style TargetType="Button" BasedOn="{StaticResource DefaultButtonStyle}">
    <Setter Property="Background" Value="Red" />
</Style>
```

### Triggers → VisualStateManager

WPF `Triggers`, `DataTriggers`, and `EventTriggers` are not supported. Two replacement approaches:

#### Approach 1: StateTrigger (direct DataTrigger replacement — simpler)

Use this for data-driven state changes. This is the closest equivalent to WPF `DataTrigger`:

**WPF:**
```xml
<Style TargetType="Border">
    <Style.Triggers>
        <DataTrigger Binding="{Binding IsActive}" Value="True">
            <Setter Property="Background" Value="Green" />
        </DataTrigger>
    </Style.Triggers>
</Style>
```

**WinUI 3:**
```xml
<Border x:Name="MyBorder">
    <VisualStateManager.VisualStateGroups>
        <VisualStateGroup>
            <VisualState x:Name="Active">
                <VisualState.StateTriggers>
                    <StateTrigger IsActive="{x:Bind ViewModel.IsActive, Mode=OneWay}" />
                </VisualState.StateTriggers>
                <VisualState.Setters>
                    <Setter Target="MyBorder.Background" Value="Green" />
                </VisualState.Setters>
            </VisualState>
        </VisualStateGroup>
    </VisualStateManager.VisualStateGroups>
</Border>
```

Note: `VisualStateManager` must be placed on a control inside the Window, NOT on the Window itself.

#### Approach 2: ControlTemplate (for property triggers like IsMouseOver)

Use this when replacing `<Trigger Property="IsMouseOver">` or similar control-state triggers:

**WPF:**
```xml
<Style TargetType="Button">
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="LightBlue"/>
        </Trigger>
    </Style.Triggers>
</Style>
```

**WinUI 3:**
```xml
<Style TargetType="Button">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Grid x:Name="RootGrid" Background="{TemplateBinding Background}">
                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                            <VisualState x:Name="PointerOver">
                                <VisualState.Setters>
                                    <Setter Target="RootGrid.Background" Value="LightBlue"/>
                                </VisualState.Setters>
                            </VisualState>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>
                    <ContentPresenter />
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### No Binding in Setter.Value

```xml
<!-- WPF (works) -->
<Setter Property="Foreground" Value="{Binding TextColor}"/>

<!-- WinUI 3 (does NOT work — use StaticResource) -->
<Setter Property="Foreground" Value="{StaticResource TextColorBrush}"/>
```

### Visual State Name Changes

| WPF | WinUI 3 |
|-----|---------|
| `MouseOver` | `PointerOver` |
| `Disabled` | `Disabled` |
| `Pressed` | `Pressed` |

## Visibility.Hidden — No Equivalent

WinUI only has `Visible` and `Collapsed`. There is no `Hidden`.

| WPF | WinUI 3 | Behavior |
|-----|---------|----------|
| `Visibility.Visible` | `Visibility.Visible` | Rendered and occupies layout space |
| `Visibility.Hidden` | **Not available** | Use `Opacity="0"` with `Visibility="Visible"` to hide but keep layout |
| `Visibility.Collapsed` | `Visibility.Collapsed` | Not rendered, no layout space |

## Resource Dictionary Changes

### XamlControlsResources Must Be First

> **Warning:** `XamlControlsResources` must be the **first** merged dictionary in `App.xaml`. It provides the default Fluent styles. Omitting it gives you controls with no visual appearance. Resource paths use `ms-appx:///` instead of relative paths.

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- MUST be first -->
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            <!-- Then your custom dictionaries -->
            <ResourceDictionary Source="ms-appx:///Styles/Colors.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### Window.Resources → Grid.Resources (or use Page)

WinUI 3 `Window` is NOT a `FrameworkElement` — no `Window.Resources`, `DataContext`, or `VisualStateManager`.

**Preferred approach:** Use the [Page-in-Window architecture](#recommended-page-in-window-architecture) described above. A `Page` inside the `Window` gives you full `FrameworkElement` capabilities (Resources, DataContext, x:Bind, VisualStateManager).

**Fallback** (for simple windows without a Page):

```xml
<!-- WPF -->
<Window>
    <Window.Resources>
        <SolidColorBrush x:Key="MyBrush" Color="Red"/>
    </Window.Resources>
    <Grid>...</Grid>
</Window>

<!-- WinUI 3 -->
<Window>
    <Grid>
        <Grid.Resources>
            <SolidColorBrush x:Key="MyBrush" Color="Red"/>
        </Grid.Resources>
        ...
    </Grid>
</Window>
```

### Theme Dictionaries

```xml
<ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
            <SolidColorBrush x:Key="MyBrush" Color="#FF000000"/>
        </ResourceDictionary>
        <ResourceDictionary x:Key="Dark">
            <SolidColorBrush x:Key="MyBrush" Color="#FFFFFFFF"/>
        </ResourceDictionary>
        <ResourceDictionary x:Key="HighContrast">
            <SolidColorBrush x:Key="MyBrush" Color="{ThemeResource SystemColorWindowTextColor}"/>
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

## URI Scheme Changes

| WPF | WinUI 3 |
|-----|---------|
| `pack://application:,,,/MyAssembly;component/image.png` | `ms-appx:///Assets/image.png` |
| `pack://application:,,,/image.png` | `ms-appx:///image.png` |
| Relative path `../image.png` | `ms-appx:///image.png` |

Assets directory convention: `Resources/` → `Assets/<Module>/`

## Data Binding Changes

### {Binding} vs {x:Bind}

Both are available. Prefer `{x:Bind}` for compile-time safety and performance.

| Feature | `{Binding}` | `{x:Bind}` |
|---------|------------|------------|
| Default mode | `OneWay` | **`OneTime`** (explicit `Mode=OneWay` required!) |
| Context | `DataContext` | Code-behind class |
| Resolution | Runtime | Compile-time |
| Performance | Reflection-based | Compiled |
| Function binding | No | Yes |

### Binding Differences from WPF

These WPF binding patterns behave differently in WinUI 3 — review on a case-by-case basis rather than mechanically removing.

```xml
<!-- UpdateSourceTrigger: limited support in WinUI 3 -->
<TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}" />
<!-- WinUI 3: UpdateSourceTrigger=LostFocus does NOT exist; PropertyChanged is the TextBox default.
     Prefer x:Bind, which binds with PropertyChanged semantics by default for TwoWay. -->
<TextBox Text="{x:Bind ViewModel.Value, Mode=TwoWay}" />

<!-- RelativeSource: Self and TemplatedParent ARE supported in WinUI 3 -->
<TextBlock Text="{Binding Tag, RelativeSource={RelativeSource Self}}" />
<!-- Works in WinUI 3. FindAncestor mode is NOT supported — use ElementName, x:Bind,
     or the CommunityToolkit FrameworkElementExtensions.Ancestor attached property to reach ancestors. -->

<!-- {Binding} empty path: works in WinUI 3 (binds to the current DataContext) -->
<ItemsControl ItemsSource="{Binding}" />
<!-- This is valid. Note: x:Bind requires an explicit path — there is no empty-path x:Bind. -->
<ItemsControl ItemsSource="{x:Bind ViewModel.Items}" />
```

## WPF-Only Window Properties to Remove

These properties exist on WPF `Window` but not WinUI 3:

```xml
<!-- Remove from XAML — handle in code-behind via AppWindow API -->
SizeToContent="Height"
WindowStartupLocation="CenterScreen"
ResizeMode="NoResize"
ExtendsContentIntoTitleBar="True"  <!-- Set in code-behind -->
```

## XAML Control Property Changes

| WPF Property | WinUI 3 Property | Notes |
|-------------|-----------------|-------|
| `Focusable` | `IsTabStop` | Different name |
| `SnapsToDevicePixels` | Not available | WinUI handles pixel snapping internally |
| `UseLayoutRounding` | `UseLayoutRounding` | Same |
| `IsHitTestVisible` | `IsHitTestVisible` | Same |
| `TextBox.VerticalScrollBarVisibility` | `ScrollViewer.VerticalScrollBarVisibility` (attached) | Attached property |

## Complete Find-and-Replace Reference

Use this table for mechanical batch translation. Apply these rules consistently to every file.

### XAML Attribute Replacements

| Find | Replace With | Context |
|------|-------------|---------|
| `ContextMenu=` | `ContextFlyout=` | On any UIElement |
| `{DynamicResource ` | `{ThemeResource ` | Theme-responsive references |
| `{x:Static prefix:Resources.Key}` | `x:Uid="Key"` (with `.resw`) | Resource string — most common WPF case; mechanical `{x:Bind}` will NOT compile here |
| `{x:Static prefix:Type.Member}` | `{x:Bind prefix:Type.Member}` | Static field/property reference (function binding) |
| `Visibility="Hidden"` | `Visibility="Collapsed"` | Or use `Opacity="0"` for layout |
| `MouseLeftButtonDown` | `PointerPressed` | Event handlers |
| `MouseLeftButtonUp` | `PointerReleased` | Event handlers |
| `MouseRightButtonDown` | `RightTapped` | Or `PointerPressed` + check `IsRightButtonPressed` |
| `MouseRightButtonUp` | `PointerReleased` + check `IsRightButtonPressed` | No direct WinUI event; use `RightTapped` only for context-menu-open semantics |
| `MouseEnter` | `PointerEntered` | Event handlers |
| `MouseLeave` | `PointerExited` | Event handlers |
| `MouseMove` | `PointerMoved` | Event handlers |
| `MouseWheel` | `PointerWheelChanged` | Event handlers |
| `MouseDoubleClick` | `DoubleTapped` | Event handlers |
| `PreviewMouseDown` | `PointerPressed` | No tunneling in WinUI — remove Preview |
| `PreviewMouseUp` | `PointerReleased` | No tunneling in WinUI — remove Preview |
| `PreviewKeyDown` | `KeyDown` | No tunneling in WinUI — remove Preview |
| `PreviewKeyUp` | `KeyUp` | No tunneling in WinUI — remove Preview |
| `PreviewMouseWheel` | `PointerWheelChanged` | No tunneling in WinUI — remove Preview |
| `Focusable="True"` | `IsTabStop="True"` | Focus behavior |
| `Focusable="False"` | `IsTabStop="False"` | Focus behavior |
| `TextWrapping="WrapWithOverflow"` | `TextWrapping="Wrap"` | TextBlock, TextBox |
| `MediaElement` | `MediaPlayerElement` | Media playback |
| `clr-namespace:` | `using:` | xmlns declarations |
| `;assembly=` | (remove) | Assembly qualification not needed |

### Code-Behind Replacements

| Find | Replace With |
|------|-------------|
| `using System.Windows;` | `using Microsoft.UI.Xaml;` |
| `using System.Windows.Controls;` | `using Microsoft.UI.Xaml.Controls;` |
| `using System.Windows.Media;` | `using Microsoft.UI.Xaml.Media;` |
| `using System.Windows.Data;` | `using Microsoft.UI.Xaml.Data;` |
| `using System.Windows.Input;` | `using Microsoft.UI.Xaml.Input;` |
| `using System.Windows.Threading;` | `using Microsoft.UI.Dispatching;` |
| `using System.Windows.Shapes;` | `using Microsoft.UI.Xaml.Shapes;` |
| `using System.Windows.Markup;` | `using Microsoft.UI.Xaml.Markup;` |
| `using System.Windows.Automation;` | `using Microsoft.UI.Xaml.Automation;` |
| `using System.Windows.Media.Animation;` | `using Microsoft.UI.Xaml.Media.Animation;` |
| `using System.Windows.Documents;` | `using Microsoft.UI.Xaml.Documents;` |
| `using System.Windows.Navigation;` | `using Microsoft.UI.Xaml.Navigation;` |
| `Dispatcher.Invoke(` | `DispatcherQueue.TryEnqueue(` |
| `Dispatcher.BeginInvoke(` | `DispatcherQueue.TryEnqueue(` |
| `Dispatcher.CheckAccess()` | `DispatcherQueue.HasThreadAccess` |
| `MouseEventArgs` | `PointerRoutedEventArgs` |
| `KeyEventArgs` | `KeyRoutedEventArgs` |
| `RoutedUICommand` | `RelayCommand` (CommunityToolkit.Mvvm) |
| `CommandBinding` | Remove; bind ICommand directly |

## XAML Formatting (XamlStyler)

After migration, run XamlStyler to normalize formatting:
- Alphabetize xmlns declarations and element attributes
- Add UTF-8 BOM to all XAML files
- Normalize comment spacing: `<!-- text -->` → `<!--  text  -->`

PowerToys command: `.\.pipelines\applyXamlStyling.ps1 -Main`
