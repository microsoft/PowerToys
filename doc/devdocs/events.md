# Events

PowerToys collects limited telemetry to understand feature usage, reliability, and product quality. When adding a new telemetry event, follow the steps below to ensure the event is properly declared, documented, and available after release.

**⚠️ Important**: Telemetry must never include personal information, file paths, or user‑generated content.

## Developer Effort Overview (What to Expect)

Adding a telemetry event is a **multi-step process** that typically spans several areas of the codebase and documentation.

At a high level, developers should expect to:

1. Within one PR:
    1. Add a new telemetry event(s) to module
    1. Add the new event(s) DATA_AND_PRIVACY.md
1. Reach out to @carlos-zamora or @chatasweetie so internal scripts can process new event(s)

### Privacy Guidelines

**NEVER** log:

- User data (text, files, emails, etc.)
- File paths or filenames
- Personal information
- Sensitive system information
- Anything that could identify a specific user

DO log:

- Feature usage (which features, how often)
- Success/failure status
- Timing/performance metrics
- Error types (not error messages with user data)
- Aggregate counts

### Event Naming Convention

Follow this pattern: `UtilityName_EventDescription`

Examples:

- `ColorPicker_Session`
- `FancyZones_LayoutApplied`
- `PowerRename_Rename`
- `AdvancedPaste_FormatClicked`
- `CmdPal_ExtensionInvoked`

## Adding Telemetry Events to PowerToys

PowerToys uses ETW (Event Tracing for Windows) for telemetry in both C++ and C# modules. The telemetry system is:

- Opt-in by default (disabled since v0.86)
- Privacy-focused - never logs personal info, file paths, or user-generated content
- Controlled by registry - HKEY_CURRENT_USER\Software\Classes\PowerToys\AllowDataDiagnostics

### C++ Telemetry Implementation

**Core Components**

| File  | Purpose |
| ------------- |:-------------:|
| src\common\Telemetry\ProjectTelemetry.h     | Declares the global ETW provider g_hProvider     |
| src\common\Telemetry\TraceBase.h      | Base class with RegisterProvider(), UnregisterProvider(), and IsDataDiagnosticsEnabled() check     |
| src\common\Telemetry\TraceLoggingDefines.h      | Privacy tags and telemetry option group macros
     |

#### Pattern for C++ Modules

1. Create a `Trace` class inheriting from `telemetry::TraceBase` (src/common/Telemetry/TraceBase.h):

```c
// trace.h
#pragma once
#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void MyEvent(/* parameters */);
};
```

2. Implement events using `TraceLoggingWriteWrapper`:

```cpp
// trace.cpp
#include "trace.h"
#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::MyEvent(bool enabled)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ModuleName_EventName",           // Event name
        TraceLoggingBoolean(enabled, "Enabled"),  // Event data
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
```

**Key C++ Telemetry Macros**

| Macro  | Purpose |
| ------------- |:-------------:|
| `TraceLoggingWriteWrapper` installer\PowerToysSetupCustomActionsVNext\CustomAction.cpp      | Wraps `TraceLoggingWrite` with `IsDataDiagnosticsEnabled()` check     |
| `ProjectTelemetryPrivacyDataTag(tag)` src\common\Telemetry\TraceLoggingDefines.h     | Sets privacy classification
| `TraceLoggingBoolean/Int32/WideString(...)`      | Type-safe data logging



### C# Telemetry Implementation

**Core Components**

| File  | Purpose |
| ------------- |:-------------:|
| src\common\ManagedTelemetry\Telemetry\PowerToysTelemetry.cs      | Singleton `Log` instance with `WriteEvent<T>()` method     |
| src\common\ManagedTelemetry\Telemetry\Events\EventBase.cs      | Base class for all events (provides `EventName`, `Version`)     |
| src\common\ManagedTelemetry\Telemetry\Events\IEvent.cs      | Interface requiring `PartA_PrivTags` property     |
| src\common\Telemetry\TelemetryBase.cs      | 	Inherits from `EventSource`, defines ETW constants     |
| src\common\ManagedTelemetry\Telemetry\DataDiagnosticsSettings.cs     | Registry-based enable/disable check
     |

#### Pattern for C# Modules

1. Create an event class inheriting from `EventBase` and implementing `IEvent`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace MyModule.Telemetry
{
    [EventData]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class MyModuleEvent : EventBase, IEvent
    {
        // Event properties (logged as telemetry data)
        public string SomeProperty { get; set; }
        public int SomeValue { get; set; }

        // Required: Privacy tag
        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;

        // Optional: Set EventName in constructor (defaults to class name)
        public MyModuleEvent(string prop, int val)
        {
            EventName = "MyModule_EventName";
            SomeProperty = prop;
            SomeValue = val;
        }
    }
}
```

2. Log the event:

```csharp
PowerToysTelemetry.Log.WriteEvent(new MyModuleEvent("value", 42));
```

**Privacy Tags (C#)**

| Tag  | Use Case |
| ------------- |:-------------:|
| `PartA_PrivTags.ProductAndServiceUsage`  src\common\Telemetry\TelemetryBase.cs     | Feature usage events
| `PartA_PrivTags.ProductAndServicePerformance` src\common\Telemetry\TelemetryBase.cs     | Performance/timing events
  

### Update DATA_AND_PRIVACY.md file

Your PR must include adding the telemetry event(s) to `PowerToys/DATA_AND_PRIVACY.md`.

## Next Steps

Reach out to @carlos-zamora or @chatasweetie so internal scripts can process new event(s).
