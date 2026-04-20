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

## Style and Template Changes

### Triggers → VisualStateManager

WPF `Triggers`, `DataTriggers`, and `EventTriggers` are not supported.

**WPF:**
```xml
<Style TargetType="Button">
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="LightBlue"/>
        </Trigger>
        <DataTrigger Binding="{Binding IsEnabled}" Value="False">
            <Setter Property="Opacity" Value="0.5"/>
        </DataTrigger>
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

## Resource Dictionary Changes

### Window.Resources → Grid.Resources

WinUI 3 `Window` is NOT a `DependencyObject` — no `Window.Resources`, `DataContext`, or `VisualStateManager`.

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

### WPF-Specific Binding Features to Remove

```xml
<!-- These WPF-only features must be removed or rewritten -->
<TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged}" />
<!-- WinUI 3: UpdateSourceTrigger not needed; TextBox uses PropertyChanged by default -->
<TextBox Text="{x:Bind ViewModel.Value, Mode=TwoWay}" />

{Binding RelativeSource={RelativeSource Self}, ...}
<!-- WinUI 3: Use x:Bind which binds to the page itself, or use ElementName -->

<ItemsControl ItemsSource="{Binding}" />
<!-- WinUI 3: Must specify explicit path -->
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

## XAML Formatting (XamlStyler)

After migration, run XamlStyler to normalize formatting:
- Alphabetize xmlns declarations and element attributes
- Add UTF-8 BOM to all XAML files
- Normalize comment spacing: `<!-- text -->` → `<!--  text  -->`

PowerToys command: `.\.pipelines\applyXamlStyling.ps1 -Main`
