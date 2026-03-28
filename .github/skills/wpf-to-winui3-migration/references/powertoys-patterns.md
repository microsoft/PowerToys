# PowerToys-Specific Migration Patterns

Patterns and conventions specific to the PowerToys codebase, based on the ImageResizer migration.

## Project Structure

### Before (WPF Module)

```
src/modules/<module>/
├── <Module>UI/
│   ├── <Module>UI.csproj        # OutputType=WinExe, UseWPF=true
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── Views/
│   ├── ViewModels/
│   ├── Helpers/
│   │   ├── Observable.cs        # Custom INotifyPropertyChanged
│   │   └── RelayCommand.cs      # Custom ICommand
│   ├── Properties/
│   │   ├── Resources.resx       # WPF resource strings
│   │   ├── Resources.Designer.cs
│   │   └── InternalsVisibleTo.cs
│   └── Telemetry/
├── <Module>CLI/
│   └── <Module>CLI.csproj       # OutputType=Exe
└── tests/
```

### After (WinUI 3 Module)

```
src/modules/<module>/
├── <Module>UI/
│   ├── <Module>UI.csproj        # OutputType=WinExe, UseWinUI=true
│   ├── Program.cs               # Custom entry point (DISABLE_XAML_GENERATED_MAIN)
│   ├── app.manifest             # Single manifest file
│   ├── ImageResizerXAML/
│   │   ├── App.xaml / App.xaml.cs   # WinUI 3 App class
│   │   ├── MainWindow.xaml / .cs
│   │   └── Views/
│   ├── Converters/              # WinUI 3 IValueConverter (string language)
│   ├── ViewModels/
│   ├── Helpers/
│   │   └── ResourceLoaderInstance.cs  # Static ResourceLoader accessor
│   ├── Utilities/
│   │   └── CodecHelper.cs       # WPF→WinRT codec ID mapping (if imaging)
│   ├── Models/
│   │   └── ImagingEnums.cs      # Custom enums replacing WPF imaging enums
│   ├── Strings/
│   │   └── en-us/
│   │       └── Resources.resw   # WinUI 3 resource strings
│   └── Assets/
│       └── <Module>/
│           └── <Module>.ico     # Moved from Resources/
├── <Module>Common/              # NEW: shared library for CLI
│   └── <Module>Common.csproj    # OutputType=Library
├── <Module>CLI/
│   └── <Module>CLI.csproj       # References Common, NOT UI
└── tests/
```

### Critical: CLI Dependency Pattern

**Do NOT** create `ProjectReference` from Exe to WinExe. This causes phantom build artifacts (`.exe`, `.deps.json`, `.runtimeconfig.json`) in the root output directory.

```
WRONG:  ImageResizerCLI (Exe) → ImageResizerUI (WinExe)    ← phantom artifacts
CORRECT: ImageResizerCLI (Exe) → ImageResizerCommon (Library)
         ImageResizerUI (WinExe) → ImageResizerCommon (Library)
```

Follow the `FancyZonesCLI` → `FancyZonesEditorCommon` pattern.

### Files to Delete

| File | Reason |
|------|--------|
| `Properties/Resources.resx` | Replaced by `Strings/en-us/Resources.resw` |
| `Properties/Resources.Designer.cs` | Auto-generated; no longer needed |
| `Properties/InternalsVisibleTo.cs` | Moved to `.csproj` `<InternalsVisibleTo>` |
| `Helpers/Observable.cs` | Replaced by `CommunityToolkit.Mvvm.ObservableObject` |
| `Helpers/RelayCommand.cs` | Replaced by `CommunityToolkit.Mvvm.Input` |
| `Resources/*.ico` / `Resources/*.png` | Moved to `Assets/<Module>/` |
| WPF `.dev.manifest` / `.prod.manifest` | Replaced by single `app.manifest` |
| WPF-specific converters | Replaced by WinUI 3 converters with `string language` |

---

## MVVM Migration: Custom → CommunityToolkit.Mvvm Source Generators

### Observable Base Class → ObservableObject + [ObservableProperty]

**Before (custom Observable):**
```csharp
public class ResizeSize : Observable
{
    private int _id;
    public int Id { get => _id; set => Set(ref _id, value); }

    private ResizeFit _fit;
    public ResizeFit Fit
    {
        get => _fit;
        set
        {
            Set(ref _fit, value);
            UpdateShowHeight();
        }
    }

    private bool _showHeight = true;
    public bool ShowHeight { get => _showHeight; set => Set(ref _showHeight, value); }
    private void UpdateShowHeight() { ShowHeight = Fit == ResizeFit.Stretch || Unit != ResizeUnit.Percent; }
}
```

**After (CommunityToolkit.Mvvm source generators):**
```csharp
public partial class ResizeSize : ObservableObject  // MUST be partial
{
    [ObservableProperty]
    [JsonPropertyName("Id")]
    private int _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeight))]  // Replaces manual UpdateShowHeight()
    private ResizeFit _fit;

    // Computed property — no backing field, no manual update method
    public bool ShowHeight => Fit == ResizeFit.Stretch || Unit != ResizeUnit.Percent;
}
```

Key changes:
- Class must be `partial` for source generators
- `Observable` → `ObservableObject` (from CommunityToolkit.Mvvm)
- Manual `Set(ref _field, value)` → `[ObservableProperty]` attribute
- `PropertyChanged` dependencies → `[NotifyPropertyChangedFor(nameof(...))]`
- Computed properties with manual `UpdateXxx()` → direct expression body

### Custom Name Setter with Transform

For properties that transform the value before storing:

```csharp
// Cannot use [ObservableProperty] because of value transformation
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, ReplaceTokens(value));  // SetProperty from ObservableObject
}
```

### RelayCommand → [RelayCommand] Source Generator

```csharp
// DELETE: Helpers/RelayCommand.cs (custom ICommand)

// Before
public ICommand ResizeCommand { get; } = new RelayCommand(Execute);

// After
[RelayCommand]
private void Resize() { /* ... */ }
// Source generator creates ResizeCommand property automatically
```

---

## Resource String Migration (.resx → .resw)

### ResourceLoaderInstance Helper

```csharp
internal static class ResourceLoaderInstance
{
    internal static ResourceLoader ResourceLoader { get; private set; }

    static ResourceLoaderInstance()
    {
        ResourceLoader = new ResourceLoader("PowerToys.ImageResizer.pri");
    }
}
```

**Note**: Use the single-argument `ResourceLoader` constructor. The two-argument version (`ResourceLoader("file.pri", "path/Resources")`) may fail if the resource map path doesn't match the actual PRI structure.

### Usage

```csharp
// WPF
using ImageResizer.Properties;
string text = Resources.MyStringKey;

// WinUI 3
string text = ResourceLoaderInstance.ResourceLoader.GetString("MyStringKey");
```

### Lazy Initialization for Resource-Dependent Statics

`ResourceLoader` is not available at class-load time in all contexts (CLI mode, test harness). Use lazy initialization:

**Before (crashes at class load):**
```csharp
private static readonly CompositeFormat _format =
    CompositeFormat.Parse(Resources.Error_Format);

private static readonly Dictionary<string, string> _tokens = new()
{
    ["$small$"] = Resources.Small,
    ["$medium$"] = Resources.Medium,
};
```

**After (lazy, safe):**
```csharp
private static CompositeFormat _format;
private static CompositeFormat Format => _format ??=
    CompositeFormat.Parse(ResourceLoaderInstance.ResourceLoader.GetString("Error_Format"));

private static readonly Lazy<Dictionary<string, string>> _tokens = new(() =>
    new Dictionary<string, string>
    {
        ["$small$"] = ResourceLoaderInstance.ResourceLoader.GetString("Small"),
        ["$medium$"] = ResourceLoaderInstance.ResourceLoader.GetString("Medium"),
    });
// Usage: _tokens.Value.TryGetValue(...)
```

### XAML: x:Static → x:Uid

```xml
<!-- WPF -->
<Button Content="{x:Static p:Resources.Cancel}" />
<!-- WinUI 3 -->
<Button x:Uid="Cancel" />
```

In `.resw`, use property-suffixed keys: `Cancel.Content`, `Header.Text`, etc.

---

## CLI Options Migration

`System.CommandLine.Option<T>` constructor signature changed:

```csharp
// WPF era — string[] aliases
public DestinationOption()
    : base(_aliases, Properties.Resources.CLI_Option_Destination)

// WinUI 3 — single string name
public DestinationOption()
    : base(_aliases[0], ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Destination"))
```

---

## Installer Updates

### WiX Changes

#### 1. Remove Satellite Assembly References

Remove from `installer/PowerToysSetupVNext/Resources.wxs`:
- `<Component>` entries for `<Module>.resources.dll`
- `<RemoveFolder>` entries for locale directories
- Module from `WinUI3AppsInstallFolder` `ParentDirectory` loop

#### 2. Update File Component Generation

Run `generateAllFileComponents.ps1` after migration. For Exe→WinExe dependency issues, add cleanup logic:

```powershell
# Strip phantom ImageResizer files from BaseApplications.wxs
$content = $content -replace 'PowerToys\.ImageResizer\.exe', ''
$content = $content -replace 'PowerToys\.ImageResizer\.deps\.json', ''
$content = $content -replace 'PowerToys\.ImageResizer\.runtimeconfig\.json', ''
```

#### 3. Output Directory

WinUI 3 modules output to `WinUI3Apps/`:
```xml
<OutputPath>..\..\..\..\$(Platform)\$(Configuration)\WinUI3Apps\</OutputPath>
```

### ESRP Signing

Update `.pipelines/ESRPSigning_core.json` — all module binaries must use `WinUI3Apps\\` paths:

```json
{
    "FileList": [
        "WinUI3Apps\\PowerToys.ImageResizer.exe",
        "WinUI3Apps\\PowerToys.ImageResizerExt.dll",
        "WinUI3Apps\\PowerToys.ImageResizerContextMenu.dll"
    ]
}
```

---

## Build Pipeline Fixes

### $(SolutionDir) → $(MSBuildThisFileDirectory)

`$(SolutionDir)` is empty when building individual projects outside the solution. Replace with relative paths from the project file:

```xml
<!-- Before (breaks on standalone project build) -->
<Exec Command="powershell $(SolutionDir)tools\build\convert-resx-to-rc.ps1" />

<!-- After (works always) -->
<Exec Command="powershell $(MSBuildThisFileDirectory)..\..\..\..\tools\build\convert-resx-to-rc.ps1" />
```

### MSIX Packaging: PreBuild → PostBuild

MSIX packaging must happen AFTER the build (artifacts not ready at PreBuild):

```xml
<!-- Before -->
<PreBuildEvent>MakeAppx.exe pack /d . /p "$(OutDir)Package.msix" /o</PreBuildEvent>

<!-- After -->
<PostBuildEvent>
  if exist "$(OutDir)Package.msix" del "$(OutDir)Package.msix"
  MakeAppx.exe pack /d "$(MSBuildThisFileDirectory)." /p "$(OutDir)Package.msix" /o
</PostBuildEvent>
```

### RC File Icon Path Escaping

Windows Resource Compiler requires double-backslash paths:

```c
// Before (breaks)
IDI_ICON1 ICON "..\\ui\Assets\ImageResizer\ImageResizer.ico"
// After
IDI_ICON1 ICON "..\\ui\\Assets\\ImageResizer\\ImageResizer.ico"
```

### BOM/Encoding Normalization

Migration may strip UTF-8 BOM from C# files (`﻿// Copyright` → `// Copyright`). This is cosmetic and safe, but be aware it will show as changes in diff.

---

## Test Adaptation

### Tests Requiring WPF Runtime

If tests still need WPF types (e.g., comparing old vs new output), temporarily add:
```xml
<UseWPF>true</UseWPF>
```
Remove this after fully migrating all test code to WinRT APIs.

### Tests Using ResourceLoader

Unit tests cannot easily initialize WinUI 3 `ResourceLoader`. Options:
- Hardcode expected strings in tests: `"Value must be between '{0}' and '{1}'."`
- Delete tests that only verify resource string lookup
- Avoid creating `App` instances in test harness (WinUI App cannot be instantiated in tests)

### Async Test Methods

All imaging tests become async:
```csharp
// Before
[TestMethod]
public void ResizesImage() { ... }

// After
[TestMethod]
public async Task ResizesImageAsync() { ... }
```

### uint Assertions

```csharp
// Before
Assert.AreEqual(96, image.Frames[0].PixelWidth);
// After
Assert.AreEqual(96u, decoder.PixelWidth);
```

### Pixel Data Access in Tests

```csharp
// Before (WPF)
public static Color GetFirstPixel(this BitmapSource source)
{
    var pixel = new byte[4];
    new FormatConvertedBitmap(
        new CroppedBitmap(source, new Int32Rect(0, 0, 1, 1)),
        PixelFormats.Bgra32, null, 0).CopyPixels(pixel, 4, 0);
    return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
}

// After (WinRT)
public static async Task<(byte R, byte G, byte B, byte A)> GetFirstPixelAsync(
    this BitmapDecoder decoder)
{
    using var bitmap = await decoder.GetSoftwareBitmapAsync(
        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    var buffer = new Windows.Storage.Streams.Buffer(
        (uint)(bitmap.PixelWidth * bitmap.PixelHeight * 4));
    bitmap.CopyToBuffer(buffer);
    using var reader = DataReader.FromBuffer(buffer);
    byte b = reader.ReadByte(), g = reader.ReadByte(),
         r = reader.ReadByte(), a = reader.ReadByte();
    return (r, g, b, a);
}
```

### Metadata Assertions

```csharp
// Before
Assert.AreEqual("Test", ((BitmapMetadata)image.Frames[0].Metadata).Comment);

// After
var props = await decoder.BitmapProperties.GetPropertiesAsync(
    new[] { "System.Photo.DateTaken" });
Assert.IsTrue(props.ContainsKey("System.Photo.DateTaken"),
    "Metadata should be preserved during transcode");
```

### AllowUnsafeBlocks for SoftwareBitmap Tests

If tests access pixel data via `IMemoryBufferByteAccess`, add:
```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

---

## Settings JSON Backward Compatibility

- Settings are stored in `%LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\`
- Schema must remain backward-compatible across upgrades
- Add new fields with defaults; never remove or rename existing fields
- Create custom enums matching WPF enum integer values for deserialization (e.g., `ImagingEnums.cs`)
- See: `src/settings-ui/Settings.UI.Library/`

## IPC Contract

If the module communicates with the runner or settings UI:
1. Update BOTH sides of the IPC contract
2. Test settings changes are received by the module
3. Test module state changes are reflected in settings UI
4. Reference: `doc/devdocs/core/settings/runner-ipc.md`

---

## Checklist for PowerToys Module Migration

### Project & Dependencies
- [ ] Update `.csproj`: `UseWPF` → `UseWinUI`, TFM → `net8.0-windows10.0.19041.0`
- [ ] Add `WindowsPackageType=None`, `SelfContained=true`, `WindowsAppSDKSelfContained=true`
- [ ] Add `DISABLE_XAML_GENERATED_MAIN` if using custom `Program.cs`
- [ ] Replace NuGet packages (WPF-UI → remove, add WindowsAppSDK, etc.)
- [ ] Update project references (GPOWrapperProjection → GPOWrapper + CsWinRT)
- [ ] Move `InternalsVisibleTo` from code to `.csproj`
- [ ] Extract CLI shared logic to Library project (avoid Exe→WinExe dependency)

### MVVM & Resources
- [ ] Replace custom `Observable`/`RelayCommand` with CommunityToolkit.Mvvm source generators
- [ ] Migrate `.resx` → `.resw` (`Properties/Resources.resx` → `Strings/en-us/Resources.resw`)
- [ ] Create `ResourceLoaderInstance` helper
- [ ] Wrap resource-dependent statics in `Lazy<T>` or null-coalescing properties
- [ ] Delete `Properties/Resources.Designer.cs`, `Observable.cs`, `RelayCommand.cs`

### XAML
- [ ] Replace `clr-namespace:` → `using:` in all xmlns declarations
- [ ] Remove WPF-UI (Lepo) xmlns and controls — use native WinUI 3
- [ ] Replace `{x:Static p:Resources.Key}` → `x:Uid` with `.resw` keys
- [ ] Replace `{DynamicResource}` → `{ThemeResource}`
- [ ] Replace `DataType="{x:Type ...}"` → `x:DataType="..."`
- [ ] Replace `<Style.Triggers>` → `VisualStateManager`
- [ ] Add `<XamlControlsResources/>` to `App.xaml` merged dictionaries
- [ ] Move `Window.Resources` to root container's `Resources`
- [ ] Run XamlStyler: `.\.pipelines\applyXamlStyling.ps1 -Main`

### Code-Behind & APIs
- [ ] Replace all `System.Windows.*` namespaces with `Microsoft.UI.Xaml.*`
- [ ] Replace `Dispatcher` with `DispatcherQueue`
- [ ] Store `DispatcherQueue` reference explicitly (no `Application.Current.Dispatcher`)
- [ ] Implement `SizeToContent()` via AppWindow if needed
- [ ] Update `ContentDialog` calls to set `XamlRoot`
- [ ] Update `FilePicker` calls with HWND initialization
- [ ] Migrate imaging code to `Windows.Graphics.Imaging` (async, `SoftwareBitmap`)
- [ ] Create `CodecHelper` for legacy GUID → WinRT codec ID mapping (if imaging)
- [ ] Create custom imaging enums for JSON backward compatibility (if imaging)
- [ ] Update all `IValueConverter` signatures (`CultureInfo` → `string`)

### Build & Installer
- [ ] Update WiX installer: remove satellite assembly refs from `Resources.wxs`
- [ ] Run `generateAllFileComponents.ps1`; handle phantom artifacts
- [ ] Update ESRP signing paths to `WinUI3Apps\\`
- [ ] Fix `$(SolutionDir)` → `$(MSBuildThisFileDirectory)` in build events
- [ ] Move MSIX packaging from PreBuild to PostBuild
- [ ] Fix RC file path escaping (double-backslash)
- [ ] Verify output dir is `WinUI3Apps/`

### Testing & Validation
- [ ] Update test project: async methods, `uint` assertions
- [ ] Handle ResourceLoader unavailability in tests (hardcode strings or skip)
- [ ] Build clean: `cd` to project folder, `tools/build/build.cmd`, exit code 0
- [ ] Run tests for affected module
- [ ] Verify settings JSON backward compatibility
- [ ] Test IPC contracts (runner ↔ settings UI)
