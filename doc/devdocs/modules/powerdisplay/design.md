# PowerDisplay Module Design Document

## Table of Contents

1. [Background](#background)
2. [Problem Statement](#problem-statement)
3. [Goals and Non-Goals](#goals-and-non-goals)
4. [Technical Terminology](#technical-terminology)
5. [Architecture Overview](#architecture-overview)
6. [Component Design](#component-design)
   - [Monitor Identification: Handles, IDs, and Names](#monitor-identification-handles-ids-and-names)
7. [Data Flow and Communication](#data-flow-and-communication)
8. [Sequence Diagrams](#sequence-diagrams)
9. [Data Models](#data-models)
10. [Future Considerations](#future-considerations)

---

## Background

PowerDisplay is a PowerToys module designed to provide unified control over display
settings across multiple monitors. Users often work with multiple displays (external monitors or laptop screens) and need a
convenient way to adjust display parameters such as brightness, contrast, color
temperature, volume, and input source without navigating through individual monitor
OSD menus.

The module leverages two primary technologies for monitor control:

1. **DDC/CI (Display Data Channel Command Interface)** - For external monitors
2. **WMI (Windows Management Instrumentation)** - For internal(laptop) displays

---

## Problem Statement

Users with multiple monitors face several challenges:

1. **Fragmented Control**: Each monitor requires separate OSD navigation
2. **Inconsistent Brightness**: Difficult to maintain uniform brightness across displays
3. **No Profile Support**: Cannot quickly switch display configurations for different
   scenarios (gaming, productivity, movie watching)
4. **Theme Integration Gap**: No automatic display adjustment when switching between
   light and dark themes

---

## Goals

- Provide unified control for brightness, contrast, volume, color temperature, and
  input source across all connected monitors
- Support both DDC/CI (external monitors) and WMI (laptop displays)
- Support user-defined profiles for quick configuration switching
- Integrate with LightSwitch module for automatic profile application on theme changes
- Support global hotkey activation

---

## Technical Terminology

### DDC/CI (Display Data Channel Command Interface)

**DDC/CI** is a VESA standard (defined in the DDC specification) that allows
bidirectional communication between a computer and a display over the I2C bus
embedded in display cables.

Most external monitors support DDC/CI, allowing applications to read and modify settings
like brightness and contrast programmatically. But unfortunately, some manufacturers have poor implementations of their product's driver. They may not support DDC/CI or report itself supports DDC/CI (through capabilities string) when it does not. Even if a monitor supports DDC/CI, they may only support a limited subset of VCP codes, or have buggy implementations.

And sometimes, users may connect monitor through a KVM switch or docking station that does not pass through DDC/CI commands correctly, and their docking may report it supports (hard code a capabilities string) but in reality, it does not. And will do thing when we try to send DDC/CI commands.

PowerDisplay relies on the monitor-reported capabilities string to determine supported features. But if your monitor's manufacturer has a poor DDC/CI implementation, or you are connecting through a docking station that does not properly support DDC/CI, some features may not work as expected. And we can do nothing about it.

**Key Concepts:**

| Term | Description |
|------|-------------|
| **VCP (Virtual Control Panel)** | Standardized codes for monitor settings |
| **MCCS (Monitor Command Control Set)** | VESA standard defining VCP codes |
| **Capabilities String** | Monitor-reported string describing supported features |

**Common VCP Codes Used:**

| VCP Code | Name | Description |
|----------|------|-------------|
| `0x10` | Brightness | Display luminance (0-100) |
| `0x12` | Contrast | Display contrast ratio (0-100) |
| `0x14` | Select Color Preset | Color temperature presets (sRGB, 5000K, 6500K, etc.) |
| `0x60` | Input Source | Active video input (HDMI, DP, USB-C, etc.) |
| `0x62` | Volume | Speaker/headphone volume (0-100) |

**Official Documentation:**
- [VESA DDC/CI Standard](https://vesa.org/vesa-standards/)


---

### WMI (Windows Management Instrumentation)

**WMI** is Microsoft's implementation of Web-Based Enterprise Management (WBEM),
providing a standardized interface for accessing management information in Windows.
For display control, WMI is primarily used for laptop internal displays that may not
support DDC/CI.

**Official Documentation:**
- [WMI Reference](https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-reference)
- [WmiMonitorBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness)

---

## Architecture Overview

### High-Level Component Architecture

```mermaid
flowchart TB
    subgraph PowerToys["PowerToys Application"]
        Runner["Runner (PowerToys.exe)"]
        SettingsUI["Settings UI (WinUI 3)"]
        LightSwitch["LightSwitch Module"]
    end

    subgraph PowerDisplayModule["PowerDisplay Module"]
        ModuleInterface["Module Interface<br/>(PowerDisplayModuleInterface.dll)"]
        PowerDisplayApp["PowerDisplay App<br/>(PowerToys.PowerDisplay.exe)"]
        PowerDisplayLib["PowerDisplay.Lib<br/>(Shared Library)"]
    end

    subgraph External["External"]
        Hardware["Display Hardware<br/>(External + Internal)"]
        Storage["Persistent Storage<br/>(settings.json, profiles.json)"]
    end

    Runner -->|"Loads DLL"| ModuleInterface
    Runner -->|"Hotkey Events"| ModuleInterface
    SettingsUI <-->|"Named Pipes"| Runner
    SettingsUI -->|"Custom Actions<br/>(Launch, ApplyColorTemperature,<br/>ApplyProfile)"| ModuleInterface

    ModuleInterface <-->|"Windows Events<br/>(Show/Toggle/Terminate)"| PowerDisplayApp
    PowerDisplayApp -->|"RefreshMonitors Event"| SettingsUI
    LightSwitch -->|"Theme Events<br/>(Light/Dark)"| PowerDisplayApp

    PowerDisplayApp --> PowerDisplayLib
    PowerDisplayLib -->|"DDC/CI (Dxva2.dll)"| Hardware
    PowerDisplayLib -->|"WMI (WmiLight)"| Hardware
    PowerDisplayLib -->|"ChangeDisplaySettingsEx"| Hardware
    PowerDisplayApp <--> Storage

    style Runner fill:#e1f5fe
    style SettingsUI fill:#e1f5fe
    style LightSwitch fill:#e1f5fe
    style ModuleInterface fill:#fff3e0
    style PowerDisplayApp fill:#fff3e0
    style PowerDisplayLib fill:#e8f5e9
    style Hardware fill:#f3e5f5
    style Storage fill:#fffde7
```

This high-level view shows the module boundaries. See [Component Design](#component-design)
for internal structure details.

---

### Project Structure

```
src/modules/powerdisplay/
├── PowerDisplay.Lib/                 # Core library (shared)
│   ├── Drivers/
│   │   ├── DDC/
│   │   │   ├── DdcCiController.cs    # DDC/CI implementation
│   │   │   ├── DdcCiNative.cs        # P/Invoke declarations & QueryDisplayConfig
│   │   │   ├── MonitorDiscoveryHelper.cs
│   │   │   └── PhysicalMonitorHandleManager.cs
│   │   ├── WMI/
│   │   │   └── WmiController.cs      # WMI implementation (WmiLight library)
│   │   ├── NativeConstants.cs        # Win32 constants (VCP codes, etc.)
│   │   ├── NativeDelegates.cs        # P/Invoke delegate types
│   │   ├── NativeStructures.cs       # Win32 structures
│   │   └── PInvoke.cs                # P/Invoke declarations
│   ├── Interfaces/
│   │   ├── IMonitorController.cs     # Controller abstraction
│   │   ├── IMonitorData.cs           # Monitor data interface
│   │   └── IProfileService.cs        # Profile service interface
│   ├── Models/
│   │   ├── Monitor.cs                # Runtime monitor data
│   │   ├── MonitorCapabilities.cs    # Monitor capability flags
│   │   ├── MonitorOperationResult.cs # Operation result
│   │   ├── MonitorStateEntry.cs      # Persisted monitor state
│   │   ├── MonitorStateFile.cs       # State file schema
│   │   ├── PowerDisplayProfile.cs    # Profile definition
│   │   ├── PowerDisplayProfiles.cs   # Profile collection
│   │   ├── ProfileMonitorSetting.cs  # Per-monitor profile settings
│   │   ├── ProfileOperation.cs       # Profile operation for IPC
│   │   ├── ColorTemperatureOperation.cs  # Color temp operation for IPC
│   │   ├── ColorPresetItem.cs        # Color preset UI item
│   │   ├── VcpCapabilities.cs        # Parsed VCP capabilities
│   │   └── VcpFeatureValue.cs        # VCP feature value (current/min/max)
│   ├── Serialization/
│   │   └── ProfileSerializationContext.cs  # JSON source generation
│   ├── Services/
│   │   ├── DisplayRotationService.cs # Display rotation via ChangeDisplaySettingsEx
│   │   ├── MonitorStateManager.cs    # State persistence (debounced)
│   │   └── ProfileService.cs         # Profile persistence
│   ├── Utils/
│   │   ├── ColorTemperatureHelper.cs # Color temp utilities
│   │   ├── EventHelper.cs            # Windows Event utilities
│   │   ├── MccsCapabilitiesParser.cs # DDC/CI capabilities parser
│   │   ├── MonitorFeatureHelper.cs   # Monitor feature utilities
│   │   ├── MonitorMatchingHelper.cs  # Profile-to-monitor matching
│   │   ├── MonitorValueConverter.cs  # Value conversion utilities
│   │   ├── PnpIdHelper.cs            # PnP manufacturer ID lookup
│   │   ├── ProfileHelper.cs          # Profile helper utilities
│   │   ├── SimpleDebouncer.cs        # Generic debouncer
│   │   └── VcpNames.cs               # VCP code and value name lookup
│   └── PathConstants.cs              # File path constants
│
├── PowerDisplay/                     # WinUI 3 application
│   ├── Assets/                       # App icons and images
│   ├── Configuration/
│   │   └── AppConstants.cs           # Application constants
│   ├── Helpers/
│   │   ├── DisplayChangeWatcher.cs   # Monitor hot-plug detection (WinRT DeviceWatcher)
│   │   ├── MonitorManager.cs         # Discovery orchestrator
│   │   ├── NativeEventWaiter.cs      # Windows Event waiting
│   │   ├── ResourceLoaderInstance.cs # Resource loader singleton
│   │   ├── SettingsDeepLink.cs       # Deep link to Settings UI
│   │   ├── TrayIconService.cs        # System tray integration
│   │   ├── TypePreservation.cs       # AOT type preservation
│   │   └── WindowHelper.cs           # Window utilities
│   ├── PowerDisplayXAML/
│   │   ├── App.xaml / App.xaml.cs    # Application entry point
│   │   ├── MainWindow.xaml / .cs     # Main UI window
│   │   ├── IdentifyWindow.xaml / .cs # Monitor identify overlay
│   │   └── MonitorIcon.xaml / .cs    # Monitor icon control
│   ├── Serialization/
│   │   └── JsonSourceGenerationContext.cs  # JSON source generation
│   ├── Services/
│   │   └── LightSwitchService.cs     # LightSwitch theme change handling
│   ├── Strings/                      # Localization resources (en-us)
│   ├── Telemetry/
│   │   └── Events/
│   │       └── PowerDisplayStartEvent.cs  # Telemetry event
│   ├── ViewModels/
│   │   ├── InputSourceItem.cs        # Input source dropdown item
│   │   ├── MainViewModel.cs          # Main VM (partial class)
│   │   ├── MainViewModel.Monitors.cs # Monitor discovery methods
│   │   ├── MainViewModel.Settings.cs # Settings persistence methods
│   │   └── MonitorViewModel.cs       # Per-monitor VM
│   ├── GlobalUsings.cs               # Global using directives
│   └── Program.cs                    # Application entry point
│
├── PowerDisplay.Lib.UnitTests/       # Unit tests
│   ├── MccsCapabilitiesParserTests.cs
│   └── MonitorMatchingHelperTests.cs
│
└── PowerDisplayModuleInterface/      # C++ DLL (module interface)
    ├── dllmain.cpp                   # PowertoyModuleIface impl
    ├── Constants.h                   # Module constants (event names, timeouts)
    ├── resource.h                    # Resource definitions
    ├── pch.h / pch.cpp               # Precompiled headers
    └── Trace.h / Trace.cpp           # ETW telemetry tracing
```

---

## Component Design

### PowerDisplay Module Internal Structure

```mermaid
flowchart TB
    subgraph ExternalInputs["External Inputs"]
        ModuleInterface["Module Interface<br/>(C++ DLL)"]
        LightSwitch["LightSwitch Module"]
    end

    subgraph WindowsEvents["Windows Events (IPC)"]
        direction LR
        ShowToggleEvents["Show/Toggle/Terminate<br/>Events"]
        ThemeChangedEvent["ThemeChanged<br/>Events"]
    end

    subgraph PowerDisplayModule["PowerDisplay Module"]
        subgraph PowerDisplayApp["PowerDisplay App (WinUI 3)"]
            MainViewModel
            MonitorViewModel
            MonitorManager
            DisplayChangeWatcher["DisplayChangeWatcher<br/>(Hot-Plug Detection)"]
            LightSwitchService["LightSwitchService<br/>(Theme Handler)"]
        end

        subgraph PowerDisplayLib["PowerDisplay.Lib"]
            subgraph Services
                ProfileService
                MonitorStateManager
                DisplayRotationService
            end
            subgraph Drivers
                DdcCiController
                WmiController
            end
            subgraph Utils
                PnpIdHelper["PnpIdHelper<br/>(Manufacturer Names)"]
            end
        end
    end

    subgraph Storage["Persistent Storage"]
        SettingsJson[("settings.json")]
        ProfilesJson[("profiles.json")]
        MonitorStateJson[("monitor_state.json")]
    end

    subgraph Hardware["Display Hardware"]
        ExternalMonitor["External Monitor"]
        LaptopDisplay["Laptop Display"]
    end

    %% External to Windows Events
    ModuleInterface -->|"SetEvent()"| ShowToggleEvents
    LightSwitch -->|"SetEvent()"| ThemeChangedEvent

    %% Windows Events to App
    ShowToggleEvents --> MainViewModel
    ThemeChangedEvent --> LightSwitchService

    %% App internal
    LightSwitchService -.->|"Get profile name"| MainViewModel
    MainViewModel --> MonitorViewModel
    MonitorViewModel --> MonitorManager
    DisplayChangeWatcher -.->|"DisplayChanged event"| MainViewModel

    %% App to Lib services
    MainViewModel --> ProfileService
    MonitorViewModel --> MonitorStateManager
    MonitorManager --> Drivers
    MonitorManager --> DisplayRotationService

    %% Utils used during discovery
    WmiController --> PnpIdHelper

    %% Services to Storage
    ProfileService --> ProfilesJson
    MonitorStateManager --> MonitorStateJson

    %% Drivers to Hardware
    DdcCiController -->|"DDC/CI"| ExternalMonitor
    WmiController -->|"WMI"| LaptopDisplay
    DisplayRotationService -->|"ChangeDisplaySettingsEx"| ExternalMonitor
    DisplayRotationService -->|"ChangeDisplaySettingsEx"| LaptopDisplay

    %% Styling
    style ExternalInputs fill:#e3f2fd,stroke:#1976d2
    style WindowsEvents fill:#fce4ec,stroke:#c2185b
    style PowerDisplayModule fill:#fff8e1,stroke:#f57c00,stroke-width:2px
    style PowerDisplayApp fill:#ffe0b2,stroke:#ef6c00
    style PowerDisplayLib fill:#c8e6c9,stroke:#388e3c
    style Services fill:#a5d6a7,stroke:#2e7d32
    style Drivers fill:#ffccbc,stroke:#e64a19
    style Utils fill:#dcedc8,stroke:#689f38
    style Storage fill:#e1bee7,stroke:#8e24aa
    style Hardware fill:#b2dfdb,stroke:#00897b
```

---

### DisplayChangeWatcher - Monitor Hot-Plug Detection

The `DisplayChangeWatcher` component provides automatic detection of monitor connect/disconnect events using the WinRT DeviceWatcher API.

**Key Features:**
- Uses `DisplayMonitor.GetDeviceSelector()` to watch for display device changes
- Implements 1-second debouncing to coalesce rapid connect/disconnect events
- Triggers `DisplayChanged` event to notify `MainViewModel` for monitor list refresh
- Runs continuously after initial monitor discovery completes

**Implementation Details:**
```csharp
// Device selector for display monitors
string selector = DisplayMonitor.GetDeviceSelector();
_deviceWatcher = DeviceInformation.CreateWatcher(selector);

// Events monitored
_deviceWatcher.Added += OnDeviceAdded;      // New monitor connected
_deviceWatcher.Removed += OnDeviceRemoved;  // Monitor disconnected
_deviceWatcher.Updated += OnDeviceUpdated;  // Monitor properties changed
```

**Debouncing Strategy:**
- Each device change event schedules a `DisplayChanged` event after 1 second
- Subsequent events within the debounce window cancel the previous timer
- This prevents excessive refreshes when multiple monitors change simultaneously

---

### DDC/CI and WMI Interaction Architecture

```mermaid
flowchart TB
    subgraph Application["Application Layer"]
        MM["MonitorManager"]
    end

    subgraph Abstraction["Abstraction Layer"]
        IMC["IMonitorController Interface"]
    end

    subgraph Controllers["Controller Implementations"]
        DDC["DdcCiController"]
        WMI["WmiController"]
    end

    subgraph DDCStack["DDC/CI Stack"]
        DDCNative["DdcCiNative<br/>(P/Invoke)"]
        PhysicalMonitorMgr["PhysicalMonitorHandleManager"]
        MonitorDiscovery["MonitorDiscoveryHelper"]
        CapParser["MccsCapabilitiesParser"]

        subgraph Win32["Win32 APIs"]
            User32["User32.dll<br/>EnumDisplayMonitors<br/>GetMonitorInfo"]
            Dxva2["Dxva2.dll<br/>GetVCPFeature<br/>SetVCPFeature<br/>Capabilities"]
        end
    end

    subgraph WMIStack["WMI Stack"]
        WmiLight["WmiLight Library<br/>(Native AOT compatible,<br/>NuGet package)"]
        PnpHelper["PnpIdHelper<br/>(Manufacturer name lookup)"]

        subgraph WMIClasses["WMI Classes (root\\WMI)"]
            WmiMonBright["WmiMonitorBrightness"]
            WmiMonBrightMethods["WmiMonitorBrightnessMethods"]
        end
    end

    subgraph Hardware["Hardware Layer"]
        ExtMon["External Monitor<br/>(DDC/CI capable)"]
        LaptopMon["Laptop Display<br/>(WMI only)"]
    end

    MM --> IMC
    IMC -.-> DDC
    IMC -.-> WMI

    DDC --> DDCNative
    DDC --> PhysicalMonitorMgr
    DDC --> MonitorDiscovery
    DDC --> CapParser

    DDCNative --> User32
    DDCNative --> Dxva2
    MonitorDiscovery --> User32
    PhysicalMonitorMgr --> Dxva2

    Dxva2 -->|"I2C/DDC"| ExtMon

    WMI --> WmiLight
    WMI --> PnpHelper
    WmiLight --> WmiMonBright
    WmiLight --> WmiMonBrightMethods

    WmiMonBrightMethods -->|"WMI Provider"| LaptopMon

    style IMC fill:#bbdefb
    style DDC fill:#c8e6c9
    style WMI fill:#ffccbc
```

### IMonitorController Interface Methods

```mermaid
classDiagram
    class IMonitorController {
        <<interface>>
        +Name: string
        +DiscoverMonitorsAsync(cancellationToken) IEnumerable~Monitor~
        +GetBrightnessAsync(monitor, cancellationToken) VcpFeatureValue
        +SetBrightnessAsync(monitor, brightness, cancellationToken) MonitorOperationResult
        +SetContrastAsync(monitor, contrast, cancellationToken) MonitorOperationResult
        +SetVolumeAsync(monitor, volume, cancellationToken) MonitorOperationResult
        +GetColorTemperatureAsync(monitor, cancellationToken) VcpFeatureValue
        +SetColorTemperatureAsync(monitor, vcpValue, cancellationToken) MonitorOperationResult
        +GetInputSourceAsync(monitor, cancellationToken) VcpFeatureValue
        +SetInputSourceAsync(monitor, inputSource, cancellationToken) MonitorOperationResult
        +Dispose()
    }

    class DdcCiController {
        -_handleManager: PhysicalMonitorHandleManager
        -_discoveryHelper: MonitorDiscoveryHelper
        +Name: "DDC/CI Monitor Controller"
        +DiscoverMonitorsAsync()
        +GetBrightnessAsync(monitor)
        +SetBrightnessAsync(monitor, brightness)
        +SetContrastAsync(monitor, contrast)
        +SetVolumeAsync(monitor, volume)
        +GetColorTemperatureAsync(monitor)
        +SetColorTemperatureAsync(monitor, colorTemperature)
        +GetInputSourceAsync(monitor)
        +SetInputSourceAsync(monitor, inputSource)
        +GetCapabilitiesStringAsync(monitor) string
        -GetVcpFeatureAsync(monitor, vcpCode, featureName)
        -CollectCandidateMonitorsAsync()
        -FetchCapabilitiesInParallelAsync()
        -GetPhysicalMonitorsWithRetryAsync()
    }

    class WmiController {
        +Name: "WMI Monitor Controller"
        +DiscoverMonitorsAsync()
        +GetBrightnessAsync(monitor)
        +SetBrightnessAsync(monitor, brightness)
        +SetContrastAsync(monitor, contrast)
        +SetVolumeAsync(monitor, volume)
        +GetColorTemperatureAsync(monitor)
        +SetColorTemperatureAsync(monitor, colorTemperature)
        +GetInputSourceAsync(monitor)
        +SetInputSourceAsync(monitor, inputSource)
        -ExtractHardwareIdFromInstanceName()
        -GetMonitorDisplayInfoByHardwareId()
    }

    IMonitorController <|.. DdcCiController
    IMonitorController <|.. WmiController
```

---

### Monitor Identification: Handles, IDs, and Names

Understanding how Windows identifies monitors is critical for PowerDisplay's operation.
Different Windows APIs use different identifiers, and PowerDisplay must correlate these
to provide a unified view across DDC/CI and WMI subsystems.

#### Windows Display Subsystem Overview

```mermaid
flowchart TB
    subgraph WindowsAPIs["Windows Display APIs"]
        EnumDisplayMonitors["EnumDisplayMonitors<br/>(User32.dll)"]
        QueryDisplayConfig["QueryDisplayConfig<br/>(User32.dll)"]
        GetPhysicalMonitors["GetPhysicalMonitorsFromHMONITOR<br/>(Dxva2.dll)"]
        WmiMonitor["WMI root\\WMI<br/>(WmiLight)"]
    end

    subgraph Identifiers["Monitor Identifiers"]
        HMONITOR["HMONITOR<br/>(Logical Monitor Handle)"]
        GdiDeviceName["GDI Device Name<br/>(e.g., \\\\.\\DISPLAY1)"]
        PhysicalHandle["Physical Monitor Handle<br/>(IntPtr for DDC/CI)"]
        DevicePath["Device Path<br/>(Unique per target)"]
        HardwareId["Hardware ID<br/>(e.g., DEL41B4)"]
        InstanceName["WMI Instance Name<br/>(e.g., DISPLAY\\BOE0900\\...)"]
        MonitorNumber["Monitor Number<br/>(1-based, matches Windows Settings)"]
    end

    EnumDisplayMonitors --> HMONITOR
    HMONITOR --> GdiDeviceName
    GetPhysicalMonitors --> PhysicalHandle

    QueryDisplayConfig --> GdiDeviceName
    QueryDisplayConfig --> DevicePath
    QueryDisplayConfig --> HardwareId
    QueryDisplayConfig --> MonitorNumber

    WmiMonitor --> InstanceName
    InstanceName --> HardwareId

    style HMONITOR fill:#e3f2fd
    style GdiDeviceName fill:#fff3e0
    style PhysicalHandle fill:#c8e6c9
    style DevicePath fill:#f3e5f5
    style HardwareId fill:#ffccbc
    style InstanceName fill:#ffe0b2
    style MonitorNumber fill:#b2dfdb
```

#### Identifier Definitions

| Identifier | Source | Format | Example | Scope |
|------------|--------|--------|---------|-------|
| **HMONITOR** | `EnumDisplayMonitors` | `IntPtr` | `0x00010001` | Logical monitor (may represent multiple physical monitors in clone mode) |
| **GDI Device Name** | `GetMonitorInfo` / `QueryDisplayConfig` | String | `\\.\DISPLAY1` | Adapter output; multiple targets can share same GDI name in mirror mode |
| **Physical Monitor Handle** | `GetPhysicalMonitorsFromHMONITOR` | `IntPtr` | `0x00000B14` | DDC/CI communication handle; valid for `GetVCPFeature` / `SetVCPFeature` |
| **Device Path** | `QueryDisplayConfig` | String | `\\?\DISPLAY#DEL41B4#5&12a3b4c&0&UID123#{...}` | Unique per target; used as primary key in `MonitorDisplayInfo` |
| **Hardware ID** | EDID (via `QueryDisplayConfig`) | String | `DEL41B4` | Manufacturer (3-char PnP ID) + Product Code (4-char hex); identifies monitor model |
| **WMI Instance Name** | `WmiMonitorBrightness` | String | `DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0` | WMI object identifier; contains hardware ID in second segment |
| **Monitor Number** | `QueryDisplayConfig` path index | Integer | `1`, `2`, `3` | 1-based; matches Windows Settings → Display → "Identify" feature |

#### DDC/CI Monitor Discovery Flow

```mermaid
sequenceDiagram
    participant App as PowerDisplay
    participant Enum as EnumDisplayMonitors
    participant Info as GetMonitorInfo
    participant QDC as QueryDisplayConfig
    participant Phys as GetPhysicalMonitors
    participant DDC as DDC/CI (I2C)

    App->>Enum: EnumDisplayMonitors(callback)
    Enum-->>App: HMONITOR handles

    loop For each HMONITOR
        App->>Info: GetMonitorInfo(hMonitor)
        Info-->>App: GDI Device Name (e.g., "\\.\DISPLAY1")

        App->>Phys: GetPhysicalMonitorsFromHMONITOR(hMonitor)
        Phys-->>App: Physical Monitor Handle(s) + Description
    end

    App->>QDC: QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)
    QDC-->>App: MonitorDisplayInfo[] (DevicePath, GdiDeviceName, HardwareId, MonitorNumber)

    Note over App: Match Physical Handles to MonitorDisplayInfo<br/>using GDI Device Name

    loop For each Physical Handle
        App->>DDC: GetCapabilitiesStringLength(handle)
        DDC-->>App: Capabilities length
        App->>DDC: CapabilitiesRequestAndCapabilitiesReply(handle)
        DDC-->>App: Capabilities string (MCCS format)
    end

    Note over App: Create Monitor objects with:<br/>- Handle (Physical Monitor Handle)<br/>- MonitorNumber (from QueryDisplayConfig)<br/>- GdiDeviceName (for rotation APIs)
```

#### WMI Monitor Discovery Flow

```mermaid
sequenceDiagram
    participant App as PowerDisplay
    participant WMI as WmiLight
    participant QDC as QueryDisplayConfig
    participant PnP as PnpIdHelper

    App->>WMI: Query WmiMonitorBrightness
    WMI-->>App: InstanceName, CurrentBrightness

    Note over App: Extract HardwareId from InstanceName<br/>"DISPLAY\BOE0900\..." → "BOE0900"

    App->>QDC: GetAllMonitorDisplayInfo()
    QDC-->>App: MonitorDisplayInfo[] (keyed by DevicePath)

    Note over App: Match WMI monitor to QueryDisplayConfig<br/>by comparing HardwareId

    App->>PnP: GetBuiltInDisplayName("BOE0900")
    PnP-->>App: "BOE Built-in Display"

    Note over App: Create Monitor objects with:<br/>- InstanceName (for WMI queries)<br/>- MonitorNumber (from QueryDisplayConfig)<br/>- GdiDeviceName (for rotation APIs)
```

#### Key Relationships

##### GDI Device Name ↔ Physical Monitors

```mermaid
flowchart TB
    HMON["HMONITOR (Logical)"]

    HMON --> GDI["GetMonitorInfo()<br/>→ GDI Device Name<br/>\.DISPLAY1"]
    HMON --> GetPhys["GetPhysicalMonitorsFromHMONITOR()"]

    GetPhys --> PM0["Physical Monitor 0<br/>Handle: 0x0B14<br/>Desc: Dell U2722D"]
    GetPhys --> PM1["Physical Monitor 1<br/>Handle: 0x0B18<br/>Desc: Dell U2722D<br/>Mirror mode"]

    style HMON fill:#e3f2fd
    style PM0 fill:#fff3e0
    style PM1 fill:#fff3e0
```

In **mirror/clone mode**, multiple physical monitors share the same GDI device name.
QueryDisplayConfig returns multiple paths with the same `GdiDeviceName` but different
`DevicePath` values, allowing us to distinguish them.

##### DisplayPort Daisy Chain (MST - Multi-Stream Transport)

**Daisy chaining** allows multiple monitors to be connected in series through a single
DisplayPort output using MST (Multi-Stream Transport) technology. This creates unique
challenges for monitor identification.

```mermaid
flowchart LR
    GPU["GPU<br/>(Single DP Port)"]
    MonA["Monitor A<br/>(MST Hub)"]
    MonB["Monitor B<br/>(End)"]

    GPU -->|"DP"| MonA -->|"DP"| MonB

    subgraph Result["Result: Multiple Logical Displays"]
        D1["DISPLAY1"]
        D2["DISPLAY2"]
    end

    GPU -.-> Result

    style GPU fill:#bbdefb
    style MonA fill:#c8e6c9
    style MonB fill:#c8e6c9
    style Result fill:#fff3e0
```

**How Windows Handles MST:**

| Aspect | Behavior |
|--------|----------|
| **HMONITOR** | Each daisy-chained monitor gets its own HMONITOR |
| **GDI Device Name** | Each monitor gets a unique GDI name (e.g., `\\.\DISPLAY1`, `\\.\DISPLAY2`) |
| **Physical Monitor Handle** | Each monitor has its own physical handle for DDC/CI |
| **Device Path** | Unique for each monitor in the chain |
| **Hardware ID** | Different if monitors are different models; same if identical models |

**MST vs Clone Mode Comparison:**

| Property | MST Daisy Chain (Extended Desktop) | Clone/Mirror Mode |
|----------|-----------------------------------|-------------------|
| **HMONITOR** | Separate per monitor (HMONITOR_1, HMONITOR_2, ...) | Shared (single HMONITOR_1) |
| **GDI Device Name** | Unique per monitor (`\\.\DISPLAY1`, `\\.\DISPLAY2`, ...) | Shared (`\\.\DISPLAY1`) |
| **Physical Handle** | One per HMONITOR (A, B, C) | Multiple per HMONITOR (A, B) |
| **DevicePath** | Unique per monitor (unique1, unique2, ...) | Unique per monitor (unique1, unique2) |
| **Behavior** | Each monitor = independent logical display | Multiple monitors share same logical display |

**PowerDisplay Handling of MST:**

1. **Discovery**: `EnumDisplayMonitors` returns separate HMONITOR for each MST monitor
2. **Physical Handles**: `GetPhysicalMonitorsFromHMONITOR` returns one handle per HMONITOR
3. **Matching**: QueryDisplayConfig provides unique DevicePath for each MST target
4. **DDC/CI**: Each monitor in the chain can be controlled independently via its handle

**Identifying Same-Model Monitors in Daisy Chain:**

When multiple identical monitors are daisy-chained (same Hardware ID), PowerDisplay
distinguishes them using:

- **MonitorNumber**: Different path index in QueryDisplayConfig (1, 2, 3...)
- **DevicePath**: Unique system-generated path for each target
- **Monitor.Id**: Format `DDC_{HardwareId}_{MonitorNumber}` ensures uniqueness

Example with two identical Dell U2722D monitors:

| Monitor | Id | MonitorNumber |
|---------|-----|---------------|
| Monitor 1 | `DDC_DEL41B4_1` | 1 |
| Monitor 2 | `DDC_DEL41B4_2` | 2 |

##### Connection Mode Summary

| Mode | HMONITOR | GDI Device Name | Physical Handles | Use Case |
|------|----------|-----------------|------------------|----------|
| **Standard** (separate cables) | 1 per monitor | Unique per monitor | 1 per HMONITOR | Most common setup |
| **Clone/Mirror** | 1 shared | Shared | Multiple per HMONITOR | Presentation, duplication |
| **MST Daisy Chain** | 1 per monitor | Unique per monitor | 1 per HMONITOR | Reduced cable clutter |
| **USB-C/Thunderbolt Hub** | 1 per monitor | Unique per monitor | 1 per HMONITOR | Laptop docking |

**Key Insight**: From PowerDisplay's perspective, MST daisy chain and standard multi-cable
setups behave identically - each monitor appears as an independent display with unique
identifiers. Only clone/mirror mode requires special handling due to shared HMONITOR/GDI names.

##### Hardware ID Composition

```mermaid
flowchart TB
    HardwareId["Hardware ID: DEL41B4"]

    HardwareId --> PnpId["DEL<br/>PnP Manufacturer ID<br/>3 chars, EDID bytes 8-9"]
    HardwareId --> ProductCode["41B4<br/>Product Code<br/>4 hex chars, EDID bytes 10-11"]

    style HardwareId fill:#fff3e0
    style PnpId fill:#c8e6c9
    style ProductCode fill:#bbdefb
```

The **PnP Manufacturer ID** is a 3-character code assigned by UEFI Forum.
Common laptop display manufacturers:

| PnP ID | Manufacturer |
|--------|--------------|
| `BOE` | BOE Technology |
| `LGD` | LG Display |
| `AUO` | AU Optronics |
| `CMN` | Chi Mei Innolux |
| `SDC` | Samsung Display |
| `SHP` | Sharp |
| `LEN` | Lenovo |
| `DEL` | Dell |

##### WMI Instance Name Parsing

```mermaid
flowchart TB
    InstanceName["WMI InstanceName:<br/>DISPLAY\BOE0900\4#amp;10fd3ab1#amp;0#amp;UID265988_0"]

    InstanceName --> Seg1["Segment 1: DISPLAY<br/>Constant prefix"]
    InstanceName --> Seg2["Segment 2: BOE0900<br/>Hardware ID<br/>Used for matching with QueryDisplayConfig"]
    InstanceName --> Seg3["Segment 3: Device instance<br/>4#amp;10fd3ab1#amp;0#amp;UID265988_0"]

    style InstanceName fill:#fff3e0
    style Seg1 fill:#e0e0e0
    style Seg2 fill:#c8e6c9
    style Seg3 fill:#e0e0e0
```

##### Monitor Number (Windows Display Settings)

The `MonitorNumber` in PowerDisplay corresponds exactly to the number shown in:
- Windows Settings → System → Display → "Identify" button
- The number overlay that appears on each display

This is derived from the **path index** in `QueryDisplayConfig`:
- `paths[0]` → Monitor 1
- `paths[1]` → Monitor 2
- etc.

#### Display Rotation and GDI Device Name

The `ChangeDisplaySettingsEx` API requires the **GDI Device Name** to target a specific display:

```cpp
// Correct: Target specific display by GDI name
ChangeDisplaySettingsEx("\\.\DISPLAY2", &devMode, NULL, 0, NULL);

// Wrong: NULL affects primary display only
ChangeDisplaySettingsEx(NULL, &devMode, NULL, 0, NULL);
```

PowerDisplay stores `GdiDeviceName` in each `Monitor` object specifically for rotation operations.

#### Cross-Reference Summary

| PowerDisplay Property | DDC/CI Source | WMI Source |
|-----------------------|---------------|------------|
| `Monitor.Id` | `"DDC_{HardwareId}_{MonitorNumber}"` | `"WMI_{HardwareId}_{MonitorNumber}"` |
| `Monitor.Handle` | Physical Monitor Handle | N/A (uses InstanceName) |
| `Monitor.InstanceName` | N/A | WMI InstanceName |
| `Monitor.GdiDeviceName` | QueryDisplayConfig | QueryDisplayConfig |
| `Monitor.MonitorNumber` | QueryDisplayConfig path index | QueryDisplayConfig (matched by HardwareId) |
| `Monitor.Name` | EDID FriendlyName or Description | PnpIdHelper.GetBuiltInDisplayName() |

---

### Settings UI and PowerDisplay Interaction Architecture

```mermaid
flowchart TB
    subgraph SettingsProcess["Settings UI Process"]
        SettingsPage["PowerDisplayPage.xaml"]
        ViewModel["PowerDisplayViewModel"]
        SettingsLib["Settings.UI.Library"]

        subgraph DataModels["Data Models"]
            PowerDisplaySettings["PowerDisplaySettings"]
            MonitorInfo["MonitorInfo"]
            ProfileOperation["ProfileOperation"]
        end
    end

    subgraph RunnerProcess["Runner Process"]
        Runner["PowerToys.exe"]
        NamedPipe["Named Pipe IPC"]
        ModuleInterface["PowerDisplayModuleInterface.dll"]
    end

    subgraph PowerDisplayProcess["PowerDisplay Process"]
        App["PowerToys.PowerDisplay.exe"]
        MainVM["MainViewModel"]

        subgraph EventListeners["Event Listeners"]
            RefreshEvent["RefreshMonitors Event"]
            ApplyColorTempEvent["ApplyColorTemp Event"]
            ApplyProfileEvent["ApplyProfile Event"]
        end
    end

    subgraph FileSystem["File System"]
        SettingsJson["PowerDisplay/settings.json"]
        ProfilesJson["PowerDisplay/profiles.json"]
    end

    %% Settings UI to Runner
    SettingsPage --> ViewModel
    ViewModel --> SettingsLib
    ViewModel -->|"SendDefaultIPCMessage()"| NamedPipe
    NamedPipe --> Runner
    Runner -->|"set_config()"| ModuleInterface
    Runner -->|"call_custom_action()"| ModuleInterface

    %% Settings persistence
    ViewModel <-->|"Read/Write"| SettingsJson
    ViewModel <-->|"Read/Write"| ProfilesJson

    %% Module Interface to PowerDisplay App
    ModuleInterface -->|"SetEvent()"| RefreshEvent
    ModuleInterface -->|"SetEvent()"| ApplyColorTempEvent
    ModuleInterface -->|"SetEvent()"| ApplyProfileEvent

    %% PowerDisplay App event handling
    RefreshEvent --> MainVM
    ApplyColorTempEvent --> MainVM
    ApplyProfileEvent --> MainVM
    MainVM <-->|"Read Settings"| SettingsJson
    MainVM <-->|"Read/Write Profiles"| ProfilesJson

    style SettingsProcess fill:#e3f2fd
    style RunnerProcess fill:#fff3e0
    style PowerDisplayProcess fill:#e8f5e9
    style FileSystem fill:#fffde7
```

### Windows Events for IPC

| Event Name | Constant | Direction | Purpose |
|------------|----------|-----------|---------|
| `Local\PowerToysPowerDisplay-ShowEvent-*` | `SHOW_POWER_DISPLAY_EVENT` | Runner → App | Show window |
| `Local\PowerToysPowerDisplay-ToggleEvent-*` | `TOGGLE_POWER_DISPLAY_EVENT` | Runner → App | Toggle visibility |
| `Local\PowerToysPowerDisplay-TerminateEvent-*` | `TERMINATE_POWER_DISPLAY_EVENT` | Runner → App | Terminate process |
| `Local\PowerToysPowerDisplay-RefreshMonitorsEvent-*` | `REFRESH_POWER_DISPLAY_MONITORS_EVENT` | Settings → App | Refresh monitor list |
| `Local\PowerToysPowerDisplay-ApplyColorTemperatureEvent-*` | `APPLY_COLOR_TEMPERATURE_POWER_DISPLAY_EVENT` | Settings → App | Apply color temp |
| `Local\PowerToysPowerDisplay-ApplyProfileEvent-*` | `APPLY_PROFILE_POWER_DISPLAY_EVENT` | Settings → App | Apply profile |
| `Local\PowerToys_LightSwitch_LightTheme` | `LightSwitchLightThemeEventName` | LightSwitch → App | Apply light mode profile |
| `Local\PowerToys_LightSwitch_DarkTheme` | `LightSwitchDarkThemeEventName` | LightSwitch → App | Apply dark mode profile |

---

### LightSwitch Profile Integration Architecture

```mermaid
flowchart TB
    subgraph LightSwitchModule["LightSwitch Module (C++)"]
        StateManager["LightSwitchStateManager"]
        ThemeEval["Theme Evaluation<br/>(Time/System)"]
        LightSwitchSettings["LightSwitchSettings"]
        NotifyPD["NotifyPowerDisplay(isLight)"]
    end

    subgraph PowerDisplayModule["PowerDisplay Module (C#)"]
        subgraph App["PowerDisplay App"]
            EventWaiter["NativeEventWaiter<br/>(Background Thread)"]
            LightSwitchSvc["LightSwitchService<br/>(Static Helper)"]
            MainViewModel["MainViewModel"]
        end

        ProfileService["ProfileService"]
        MonitorVMs["MonitorViewModels"]
        Controllers["IMonitorController"]
    end

    subgraph WindowsEvents["Windows Events"]
        LightEvent["Local\\PowerToys_LightSwitch_LightTheme"]
        DarkEvent["Local\\PowerToys_LightSwitch_DarkTheme"]
    end

    subgraph FileSystem["File System"]
        LSSettingsJson["LightSwitch/settings.json<br/>{lightProfile, darkProfile}"]
        PDProfilesJson["PowerDisplay/profiles.json<br/>{profiles: [...]}"]
    end

    subgraph Hardware["Hardware"]
        Monitors["Connected Monitors"]
    end

    %% LightSwitch flow
    ThemeEval -->|"Time boundary<br/>or manual"| StateManager
    StateManager --> LightSwitchSettings
    StateManager --> NotifyPD
    NotifyPD -->|"isLight=true"| LightEvent
    NotifyPD -->|"isLight=false"| DarkEvent

    %% PowerDisplay flow - theme determined from event
    LightEvent -->|"Event signaled"| EventWaiter
    DarkEvent -->|"Event signaled"| EventWaiter
    EventWaiter -->|"isLightMode"| LightSwitchSvc
    LightSwitchSvc -->|"GetProfileForTheme()"| LSSettingsJson
    LightSwitchSvc -->|"Profile name"| MainViewModel
    MainViewModel -->|"LoadProfiles()"| ProfileService
    ProfileService <--> PDProfilesJson
    MainViewModel -->|"ApplyProfileAsync()"| MonitorVMs
    MonitorVMs --> Controllers
    Controllers --> Monitors

    style LightSwitchModule fill:#ffccbc
    style PowerDisplayModule fill:#c8e6c9
    style App fill:#a5d6a7
    style WindowsEvents fill:#e3f2fd
    style FileSystem fill:#fffde7
```

### LightSwitch Settings JSON Structure

```json
{
  "properties": {
    "apply_monitor_settings": { "value": true },
    "enable_light_mode_profile": { "value": true },
    "light_mode_profile": { "value": "Productivity" },
    "enable_dark_mode_profile": { "value": true },
    "dark_mode_profile": { "value": "Night Mode" }
  }
}
```

---

## Data Flow and Communication

### Monitor Discovery Flow

```mermaid
flowchart TB
    Start([Start Discovery]) --> Init["MonitorManager.DiscoverMonitorsAsync()"]

    Init --> ParallelDiscover["Parallel Discovery"]

    subgraph ParallelDiscover["Parallel Controller Discovery"]
        DDCDiscover["DdcCiController.DiscoverMonitorsAsync()"]
        WMIDiscover["WmiController.DiscoverMonitorsAsync()"]
    end

    DDCDiscover --> DDCEnum["EnumDisplayMonitors()"]
    DDCEnum --> DDCPhysical["GetPhysicalMonitorsFromHMONITOR()"]
    DDCPhysical --> DDCCheck["Quick DDC/CI connection check"]

    WMIDiscover --> WMIQuery["Query WmiMonitorBrightness"]
    WMIQuery --> WMIFilter["Filter responsive displays"]

    DDCCheck --> Merge["Merge Results"]
    WMIFilter --> Merge

    Merge --> InitLoop["For Each Monitor"]

    subgraph InitLoop["Initialize Single Monitor"]
        direction TB
        VerifyControl["Verify Controller Access"]
        GetBrightness["Get Current Brightness"]
        CheckType{"CommunicationMethod<br/>contains 'DDC'?"}

        subgraph DDCPath[" "]
            direction TB
            GetCaps["Fetch VCP Capabilities"]
            ParseCaps["Parse MCCS Capabilities String"]
            InitInputSource["Get Current Input Source"]
        end

        Done["Initialization Complete"]
    end

    VerifyControl --> GetBrightness
    GetBrightness --> CheckType
    CheckType -->|"Yes (DDC/CI)"| GetCaps
    GetCaps --> ParseCaps
    ParseCaps --> InitInputSource
    InitInputSource --> Done
    CheckType -->|"No (WMI)"| Done

    InitLoop --> UpdateCollection["Update _monitors Collection"]
    UpdateCollection --> FireEvent["Fire MonitorsChanged Event"]
    FireEvent --> StartWatcher["Start DisplayChangeWatcher"]
    StartWatcher --> End([Discovery Complete])

    style ParallelDiscover fill:#e3f2fd
    style InitLoop fill:#e8f5e9
    style CheckType fill:#fff3e0
```

**Note:** WMI monitors skip VCP capabilities fetching because:
1. WMI uses a different abstraction layer (`WmiMonitorBrightness` class)
2. `WmiController.GetCapabilitiesStringAsync()` returns an empty string
3. VCP codes are DDC/CI-specific and not applicable to WMI-controlled displays

---

## Sequence Diagrams

### Sequence: Modifying Monitor Settings in Settings UI

```mermaid
sequenceDiagram
    participant User
    participant SettingsPage as PowerDisplayPage
    participant ViewModel as PowerDisplayViewModel
    participant SettingsUtils
    participant Runner
    participant ModuleInterface as PowerDisplayModule (C++)
    participant PowerDisplayApp as PowerDisplay.exe
    participant MonitorManager
    participant Controller as IMonitorController
    participant Monitor as Physical Monitor

    User->>SettingsPage: Selects color temperature<br/>from dropdown
    SettingsPage->>SettingsPage: Show confirmation dialog
    User->>SettingsPage: Confirms change

    SettingsPage->>ViewModel: ApplyColorTemperatureToMonitor(monitorId, vcpValue)

    Note over ViewModel: Store pending operation
    ViewModel->>ViewModel: _settings.Properties.PendingColorTemperatureOperation = {...}
    ViewModel->>SettingsUtils: SaveSettings(settings.json)
    SettingsUtils-->>ViewModel: Success

    Note over ViewModel: Send IPC message
    ViewModel->>Runner: SendDefaultIPCMessage(CustomAction: ApplyColorTemperature)
    Runner->>ModuleInterface: call_custom_action("ApplyColorTemperature")

    Note over ModuleInterface: Ensure process running
    ModuleInterface->>ModuleInterface: is_process_running()
    alt Process not running
        ModuleInterface->>PowerDisplayApp: launch_process()
        ModuleInterface->>ModuleInterface: wait_for_process_ready()
    end

    ModuleInterface->>PowerDisplayApp: SetEvent(ApplyColorTemperatureEvent)

    Note over PowerDisplayApp: Event listener triggers
    PowerDisplayApp->>PowerDisplayApp: ApplyColorTemperatureFromSettings()
    PowerDisplayApp->>SettingsUtils: Read settings.json
    SettingsUtils-->>PowerDisplayApp: PendingColorTemperatureOperation

    PowerDisplayApp->>MonitorManager: Find monitor by ID
    MonitorManager-->>PowerDisplayApp: Monitor found

    PowerDisplayApp->>Controller: SetColorTemperatureAsync(monitor, vcpValue)
    Controller->>Monitor: SetVCPFeature(0x14, value)
    Monitor-->>Controller: Success
    Controller-->>PowerDisplayApp: MonitorOperationResult.Success

    Note over PowerDisplayApp: Clear pending operation
    PowerDisplayApp->>SettingsUtils: Update settings.json<br/>(clear pending, update monitor value)

    Note over SettingsPage: Monitor property change<br/>notification refreshes UI
```

---

### Sequence: Creating and Saving a Profile

```mermaid
sequenceDiagram
    participant User
    participant SettingsPage as PowerDisplayPage
    participant ViewModel as PowerDisplayViewModel
    participant ProfileDialog as ProfileEditorDialog
    participant ProfileService
    participant FileSystem as profiles.json

    User->>SettingsPage: Clicks "Add Profile" button
    SettingsPage->>ViewModel: ShowProfileEditor()

    ViewModel->>ProfileDialog: Show(monitors, existingProfiles)
    ProfileDialog->>ProfileDialog: Display monitor selection UI

    User->>ProfileDialog: Enters profile name
    User->>ProfileDialog: Selects monitors to include
    User->>ProfileDialog: Configures settings per monitor<br/>(brightness, contrast, etc.)
    User->>ProfileDialog: Clicks "Save"

    ProfileDialog->>ProfileDialog: Validate inputs
    Note over ProfileDialog: Check name unique,<br/>at least one monitor selected

    ProfileDialog-->>ViewModel: ResultProfile (PowerDisplayProfile)

    ViewModel->>ProfileService: AddOrUpdateProfile(profile)

    ProfileService->>ProfileService: lock(_lock)
    ProfileService->>FileSystem: Read profiles.json
    FileSystem-->>ProfileService: Existing profiles
    ProfileService->>ProfileService: Add/update profile in collection
    ProfileService->>ProfileService: Set LastUpdated = DateTime.Now
    ProfileService->>FileSystem: Write profiles.json
    FileSystem-->>ProfileService: Success
    ProfileService-->>ViewModel: true

    ViewModel->>ViewModel: RefreshProfilesList()
    ViewModel-->>SettingsPage: PropertyChanged(Profiles)
    SettingsPage->>SettingsPage: Update UI with new profile
```

---

### Sequence: Applying Profile via LightSwitch Theme Change

```mermaid
sequenceDiagram
    participant System as Windows System
    participant LightSwitch as LightSwitchStateManager (C++)
    participant WinEvent as Windows Events
    participant EventWaiter as NativeEventWaiter
    participant LSSvc as LightSwitchService
    participant MainVM as MainViewModel
    participant ProfileService
    participant MonitorVM as MonitorViewModel
    participant Controller as IMonitorController
    participant Monitor as Physical Monitor

    Note over System: Time reaches threshold<br/>or user changes theme
    System->>LightSwitch: Theme change detected

    LightSwitch->>LightSwitch: EvaluateAndApplyIfNeeded()
    LightSwitch->>LightSwitch: ApplyTheme(isLight)

    LightSwitch->>LightSwitch: NotifyPowerDisplay(isLight)
    Note over LightSwitch: Check if profile enabled

    alt isLight == true
        LightSwitch->>WinEvent: SetEvent("Local\\PowerToys_LightSwitch_LightTheme")
    else isLight == false
        LightSwitch->>WinEvent: SetEvent("Local\\PowerToys_LightSwitch_DarkTheme")
    end

    Note over EventWaiter: Background thread waiting<br/>on both Light and Dark events
    EventWaiter->>WinEvent: WaitAny([lightEvent, darkEvent]) returns index

    Note over EventWaiter: Theme determined from event:<br/>index 0 = Light, index 1 = Dark
    EventWaiter->>LSSvc: GetProfileForTheme(isLightMode)
    LSSvc->>LSSvc: Read LightSwitch/settings.json
    LSSvc-->>EventWaiter: profileName (or null)

    EventWaiter->>MainVM: Dispatch to UI thread with profileName

    MainVM->>ProfileService: LoadProfiles()
    ProfileService-->>MainVM: PowerDisplayProfiles

    MainVM->>MainVM: Find profile by name
    MainVM->>MainVM: ApplyProfileAsync(profile.MonitorSettings)

    loop For each ProfileMonitorSetting
        MainVM->>MainVM: Find MonitorViewModel by InternalName

        alt Brightness specified
            MainVM->>MonitorVM: SetBrightnessAsync(value, immediate=true)
            MonitorVM->>Controller: SetBrightnessAsync(monitor, value)
            Controller->>Monitor: DDC/CI or WMI call
            Monitor-->>Controller: Success
        end

        alt Contrast specified
            MainVM->>MonitorVM: SetContrastAsync(value, immediate=true)
            MonitorVM->>Controller: SetContrastAsync(monitor, value)
            Controller->>Monitor: SetVCPFeature(0x12, value)
        end

        alt Volume specified
            MainVM->>MonitorVM: SetVolumeAsync(value, immediate=true)
            MonitorVM->>Controller: SetVolumeAsync(monitor, value)
            Controller->>Monitor: SetVCPFeature(0x62, value)
        end

        alt ColorTemperature specified
            MainVM->>MonitorVM: SetColorTemperatureAsync(vcpValue)
            MonitorVM->>Controller: SetColorTemperatureAsync(monitor, vcpValue)
            Controller->>Monitor: SetVCPFeature(0x14, vcpValue)
        end

        alt Orientation specified
            MainVM->>MonitorVM: SetOrientationAsync(orientation)
            MonitorVM->>Controller: SetRotationAsync(monitor, orientation)
            Controller->>Monitor: ChangeDisplaySettingsEx
        end
    end

    Note over MainVM: await Task.WhenAll(updateTasks)
    MainVM->>MainVM: Log profile application complete
```

---

### Sequence: UI Slider Adjustment (Brightness)

```mermaid
sequenceDiagram
    participant User
    participant Slider as Brightness Slider
    participant MonitorVM as MonitorViewModel
    participant Debouncer as SimpleDebouncer
    participant MonitorManager
    participant Controller as DdcCiController
    participant StateManager as MonitorStateManager
    participant Monitor as Physical Monitor

    User->>Slider: Drags slider (continuous)

    loop During drag (multiple events)
        Slider->>MonitorVM: CurrentBrightness = value
        MonitorVM->>MonitorVM: SetBrightnessAsync(value, immediate=false)
        MonitorVM->>Debouncer: Debounce(300ms)
        Note over Debouncer: Resets timer on each call
    end

    User->>Slider: Releases slider

    Note over Debouncer: 300ms elapsed, no new input
    Debouncer->>MonitorVM: Execute debounced action

    MonitorVM->>MonitorVM: ApplyBrightnessToHardwareAsync()
    MonitorVM->>MonitorManager: SetBrightnessAsync(monitor, finalValue)

    MonitorManager->>Controller: SetBrightnessAsync(monitor, value)

    Controller->>Controller: SetVcpFeatureAsync(VcpCodeBrightness)
    Controller->>Monitor: SetVCPFeature(0x10, value)
    Monitor-->>Controller: OK

    Controller-->>MonitorManager: MonitorOperationResult
    MonitorManager-->>MonitorVM: Success/Failure

    MonitorVM->>StateManager: UpdateMonitorParameter("Brightness", value)

    Note over StateManager: Debounced save (2 seconds)
    StateManager->>StateManager: Schedule file write

    Note over StateManager: After 2s idle
    StateManager->>StateManager: SaveToFile(monitor_state.json)
```

---

### Sequence: Module Enable/Disable Lifecycle

```mermaid
sequenceDiagram
    participant Runner as PowerToys Runner
    participant ModuleInterface as PowerDisplayModule (C++)
    participant PowerDisplayApp as PowerDisplay.exe
    participant MonitorManager
    participant EventHandles as Windows Events

    Note over Runner: User enables PowerDisplay
    Runner->>ModuleInterface: enable()

    ModuleInterface->>ModuleInterface: m_enabled = true
    ModuleInterface->>ModuleInterface: Trace::EnablePowerDisplay(true)

    ModuleInterface->>ModuleInterface: is_process_running()
    alt Process not running
        ModuleInterface->>PowerDisplayApp: ShellExecuteExW("PowerToys.PowerDisplay.exe", pid)
        PowerDisplayApp->>PowerDisplayApp: Initialize WinUI 3 App
        PowerDisplayApp->>PowerDisplayApp: RegisterSingletonInstance()
        PowerDisplayApp->>MonitorManager: DiscoverMonitorsAsync()
        PowerDisplayApp->>PowerDisplayApp: Start event listeners
        PowerDisplayApp->>EventHandles: SetEvent("Ready")
    end

    ModuleInterface->>ModuleInterface: m_hProcess = sei.hProcess

    Note over Runner: User presses hotkey
    Runner->>ModuleInterface: on_hotkey()
    ModuleInterface->>EventHandles: SetEvent(ToggleEvent)
    EventHandles->>PowerDisplayApp: Toggle visibility

    Note over Runner: User disables PowerDisplay
    Runner->>ModuleInterface: disable()

    ModuleInterface->>EventHandles: ResetEvent(InvokeEvent)
    ModuleInterface->>EventHandles: SetEvent(TerminateEvent)

    PowerDisplayApp->>PowerDisplayApp: Receive terminate signal
    PowerDisplayApp->>MonitorManager: Dispose()
    PowerDisplayApp->>PowerDisplayApp: Application.Exit()

    ModuleInterface->>ModuleInterface: CloseHandle(m_hProcess)
    ModuleInterface->>ModuleInterface: m_enabled = false
    ModuleInterface->>ModuleInterface: Trace::EnablePowerDisplay(false)
```

---

## Data Models

### Core Models

```mermaid
classDiagram
    class Monitor {
        +string Id
        +string Name
        +string CommunicationMethod
        +string InstanceName
        +string GdiDeviceName
        +int MonitorNumber
        +int CurrentBrightness
        +int MinBrightness
        +int MaxBrightness
        +int CurrentContrast
        +int MinContrast
        +int MaxContrast
        +int CurrentVolume
        +int MinVolume
        +int MaxVolume
        +int CurrentColorTemperature
        +string ColorTemperaturePresetName
        +int CurrentInputSource
        +string InputSourceName
        +IReadOnlyList~int~ SupportedInputSources
        +int Orientation
        +bool IsAvailable
        +bool SupportsContrast
        +bool SupportsVolume
        +bool SupportsColorTemperature
        +bool SupportsInputSource
        +MonitorCapabilities Capabilities
        +VcpCapabilities VcpCapabilitiesInfo
        +string CapabilitiesRaw
        +IntPtr Handle
        +DateTime LastUpdate
        +UpdateStatus(brightness, isAvailable)
    }

    class VcpCapabilities {
        +string Raw
        +string Model
        +string Type
        +string Protocol
        +string MccsVersion
        +List~byte~ SupportedCommands
        +Dictionary~byte, VcpCodeInfo~ SupportedVcpCodes
        +List~WindowCapability~ Windows
        +bool HasWindowSupport
        +static VcpCapabilities Empty$
        +SupportsVcpCode(code) bool
        +GetVcpCodeInfo(code) VcpCodeInfo
        +HasDiscreteValues(code) bool
        +GetSupportedValues(code) IReadOnlyList~int~
        +GetVcpCodesAsHexStrings() List~string~
        +GetSortedVcpCodes() IEnumerable~VcpCodeInfo~
    }

    class VcpCodeInfo {
        +byte Code
        +string Name
        +IReadOnlyList~int~ SupportedValues
        +bool HasDiscreteValues
        +bool IsContinuous
        +string FormattedCode
        +string FormattedTitle
    }

    class WindowCapability {
        <<struct>>
        +int WindowNumber
        +string Type
        +WindowArea Area
        +WindowSize MaxSize
        +WindowSize MinSize
        +int WindowId
    }

    class WindowSize {
        <<struct>>
        +int Width
        +int Height
    }

    class WindowArea {
        <<struct>>
        +int X1
        +int Y1
        +int X2
        +int Y2
        +int Width
        +int Height
    }

    class VcpFeatureValue {
        +int Current
        +int Minimum
        +int Maximum
        +bool IsValid
        +ToPercentage() int
        +static Invalid VcpFeatureValue
    }

    class MonitorCapabilities {
        <<flags enum>>
        None
        Brightness
        Contrast
        Volume
        ColorTemperature
        InputSource
        Wmi
        DdcCi
    }

    class PowerDisplayProfile {
        +string Name
        +DateTime CreatedDate
        +DateTime LastModified
        +List~ProfileMonitorSetting~ MonitorSettings
        +IsValid() bool
    }

    class ProfileMonitorSetting {
        +string MonitorInternalName
        +int MonitorNumber
        +int? Brightness
        +int? Contrast
        +int? Volume
        +int? ColorTemperatureVcp
        +int? Orientation
    }

    class PowerDisplayProfiles {
        +List~PowerDisplayProfile~ Profiles
        +DateTime LastUpdated
    }

    Monitor "1" --> "0..1" VcpCapabilities
    Monitor "1" --> "1" MonitorCapabilities
    VcpCapabilities "1" --> "*" VcpCodeInfo
    VcpCapabilities "1" --> "*" WindowCapability
    WindowCapability "1" --> "1" WindowArea
    WindowCapability "1" --> "1" WindowSize : MaxSize
    WindowCapability "1" --> "1" WindowSize : MinSize
    PowerDisplayProfiles "1" --> "*" PowerDisplayProfile
    PowerDisplayProfile "1" --> "*" ProfileMonitorSetting
```

### Settings Models

```mermaid
classDiagram
    class PowerDisplaySettings {
        +string Name
        +PowerDisplayProperties Properties
        +string Version
        +ToJsonString() string
    }

    class PowerDisplayProperties {
        +bool Enabled
        +bool HotkeyEnabled
        +HotkeySettings ActivationShortcut
        +string BrightnessUpdateRate
        +bool RestoreSettingsOnStartup
        +bool ShowSystemTrayIcon
        +List~MonitorInfo~ Monitors
        +ColorTemperatureOperation PendingColorTemperatureOperation
        +ProfileOperation PendingProfileOperation
    }

    class MonitorInfo {
        +string Name
        +string InternalName
        +string HardwareId
        +string CommunicationMethod
        +int MonitorNumber
        +int TotalMonitorCount
        +string DisplayName
        +string MonitorIconGlyph
        +int CurrentBrightness
        +int Contrast
        +int Volume
        +int ColorTemperatureVcp
        +int Orientation
        +bool SupportsBrightness
        +bool SupportsContrast
        +bool SupportsColorTemperature
        +bool SupportsVolume
        +bool SupportsInputSource
        +bool EnableContrast
        +bool EnableVolume
        +bool EnableInputSource
        +bool EnableRotation
        +bool IsHidden
        +string CapabilitiesRaw
        +List~string~ VcpCodes
        +List~VcpCodeDisplayInfo~ VcpCodesFormatted
        +ObservableCollection~ColorPresetItem~ AvailableColorPresets
        +ObservableCollection~ColorPresetItem~ ColorPresetsForDisplay
        +bool HasCapabilities
        +bool ShowCapabilitiesWarning
        +GetVcpCodesAsText() string
        +UpdateFrom(other)
    }

    class VcpCodeDisplayInfo {
        +string Code
        +string Title
        +string Values
        +bool HasValues
        +List~VcpValueInfo~ ValueList
    }

    class VcpValueInfo {
        +string Value
        +string Name
    }

    class ColorTemperatureOperation {
        +string MonitorId
        +int ColorTemperatureVcp
    }

    class ProfileOperation {
        +string ProfileName
        +List~ProfileMonitorSetting~ MonitorSettings
    }

    MonitorInfo "1" --> "*" VcpCodeDisplayInfo
    VcpCodeDisplayInfo "1" --> "*" VcpValueInfo
    PowerDisplaySettings "1" --> "1" PowerDisplayProperties
    PowerDisplayProperties "1" --> "*" MonitorInfo
    PowerDisplayProperties "1" --> "0..1" ColorTemperatureOperation
    PowerDisplayProperties "1" --> "0..1" ProfileOperation
```

---

## Future Considerations

### Already Implemented (removed from backlog)

- **Monitor Hot-Plug**: `DisplayChangeWatcher` uses WinRT DeviceWatcher + DisplayMonitor API with 1-second debouncing
- **Display Rotation**: `DisplayRotationService` uses Windows ChangeDisplaySettingsEx API
- **LightSwitch Integration**: Automatic profile application on theme changes via `LightSwitchService`
- **Monitor Identification**: Overlay windows showing monitor numbers via `IdentifyWindow`
- **Mirror Mode Support**: Correct orientation sync for multiple monitors sharing the same GDI device name

### Potential Future Enhancements

1. **Hardware Cursor Brightness**: Support for displays with hardware cursor brightness
2. **Multi-GPU Support**: Better handling of monitors across different GPUs
3. **Advanced Color Management**: Integration with Windows Color Management APIs (HDR, ICC profiles)
4. **Scheduled Profiles**: Time-based automatic profile switching (beyond LightSwitch integration)
5. **Monitor Groups**: Ability to control multiple monitors as a single entity
6. **Remote Control**: Network-based control for multi-system setups
7. **PIP/PBP Control**: Picture-in-Picture and Picture-by-Picture configuration (VcpCapabilities already parses window capabilities)
8. **Power State Control**: Monitor power on/off via VCP code 0xD6
9. **Input Source Scheduling**: Automatic input switching based on time or application

---

## References

- [VESA DDC/CI Standard](https://vesa.org/vesa-standards/)
- [MCCS (Monitor Control Command Set) Specification](https://vesa.org/vesa-standards/)
- [Microsoft High-Level Monitor Configuration API](https://learn.microsoft.com/en-us/windows/win32/monitor/high-level-monitor-configuration-api)
- [WMI Reference](https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-reference)
- [WmiMonitorBrightness Class](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness)
- [PowerToys Architecture Documentation](../../core/architecture.md)
