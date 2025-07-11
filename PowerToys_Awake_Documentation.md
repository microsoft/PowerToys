# PowerToys Awake - Comprehensive Documentation

## Table of Contents
1. [Overview](#overview)
2. [Key Features](#key-features)
3. [Installation](#installation)
4. [Command Line Usage](#command-line-usage)
5. [GUI Usage](#gui-usage)
6. [Operating Modes](#operating-modes)
7. [Configuration File](#configuration-file)
8. [Examples](#examples)
9. [Advanced Usage](#advanced-usage)
10. [Troubleshooting](#troubleshooting)
11. [Technical Details](#technical-details)

## Overview

PowerToys Awake is a utility designed to keep your computer awake without permanently modifying system power settings. It prevents the computer from sleeping and can optionally keep the monitor on, providing a convenient alternative to changing system power configurations.

**Application Name**: `Awake.exe` (part of PowerToys)  
**Full Name**: PowerToys Awake  
**Build Version**: TILLSON_11272024

## Key Features

- **Temporary Override**: Prevents system sleep without permanent power setting changes
- **Display Control**: Option to keep monitor on or allow it to turn off
- **Multiple Modes**: Support for indefinite, timed, expirable, and passive modes
- **Command Line Interface**: Full programmatic control via command-line parameters
- **Process Binding**: Bind Awake to another process's lifecycle
- **System Tray Integration**: Easy access through Windows system tray
- **PowerToys Integration**: Seamless integration with PowerToys settings

## Installation

Awake is included as part of Microsoft PowerToys. Install PowerToys from:
- Microsoft Store
- GitHub Releases: https://github.com/microsoft/PowerToys/releases
- Windows Package Manager: `winget install Microsoft.PowerToys`

## Command Line Usage

### Basic Syntax
```
Awake.exe [OPTIONS]
```

### Available Options

| Option | Short | Description | Type | Default |
|--------|-------|-------------|------|---------|
| `--use-pt-config` | `-c` | Use PowerToys configuration file for managing state | Boolean | false |
| `--display-on` | `-d` | Keep the display awake | Boolean | true |
| `--time-limit` | `-t` | Time interval in seconds to keep computer awake | Integer | 0 |
| `--pid` | `-p` | Bind to a specific process ID | Integer | 0 |
| `--expire-at` | `-e` | Expire at specific date/time | DateTime String | - |
| `--use-parent-pid` | `-u` | Bind to parent process | Boolean | false |

### Parameter Details

#### `--use-pt-config` / `-c`
- **Purpose**: Use PowerToys configuration file for state management
- **Behavior**: When enabled, ignores other command-line parameters
- **Usage**: `Awake.exe -c`

#### `--display-on` / `-d`
- **Purpose**: Controls whether the display stays on
- **Values**: `true` (keep display on) or `false` (allow display to turn off)
- **Usage**: `Awake.exe -d false`

#### `--time-limit` / `-t`
- **Purpose**: Keep computer awake for specified seconds
- **Range**: 0 to 4,294,967,295 seconds
- **Note**: 0 means indefinite
- **Usage**: `Awake.exe -t 3600` (1 hour)

#### `--pid` / `-p`
- **Purpose**: Bind Awake to another process
- **Behavior**: Awake terminates when the target process ends
- **Usage**: `Awake.exe -p 1234`

#### `--expire-at` / `-e`
- **Purpose**: Set expiration date and time
- **Format**: ISO 8601 date/time format
- **Usage**: `Awake.exe -e "2025-07-07T15:30:00"`

#### `--use-parent-pid` / `-u`
- **Purpose**: Bind to the parent process of Awake
- **Behavior**: Automatically determines parent PID
- **Usage**: `Awake.exe -u`

## GUI Usage

Access Awake through the PowerToys system tray icon:

1. **Right-click** the PowerToys tray icon
2. **Navigate** to Awake submenu
3. **Select** desired mode:
   - Off (Passive)
   - Keep awake indefinitely
   - Keep awake for interval
   - Keep awake until expiration

### PowerToys Settings

Open PowerToys Settings → Awake to configure:
- **Enable/Disable** Awake module
- **Mode Selection**: Passive, Indefinite, Timed, Expirable
- **Display Settings**: Keep screen on/off
- **Time Configuration**: Hours and minutes for timed mode
- **Expiration Settings**: Date and time for expirable mode

## Operating Modes

### 1. Passive Mode (`PASSIVE`)
- **Description**: Uses system's default power plan
- **Command**: `Awake.exe -c` (with passive mode in config)
- **Behavior**: No keep-awake functionality active

### 2. Indefinite Mode (`INDEFINITE`)
- **Description**: Keeps computer awake indefinitely
- **Command**: `Awake.exe -t 0` or `Awake.exe` (default)
- **Behavior**: Prevents sleep until manually stopped

### 3. Timed Mode (`TIMED`)
- **Description**: Keeps computer awake for specified duration
- **Command**: `Awake.exe -t <seconds>`
- **Behavior**: Automatically returns to passive mode after timeout

### 4. Expirable Mode (`EXPIRABLE`)
- **Description**: Keeps computer awake until specific date/time
- **Command**: `Awake.exe -e "YYYY-MM-DDTHH:MM:SS"`
- **Behavior**: Automatically returns to passive mode at expiration

## Configuration File

When using `--use-pt-config`, Awake reads settings from PowerToys configuration:

```json
{
    "properties": {
        "keepDisplayOn": true,
        "mode": 1,
        "intervalHours": 2,
        "intervalMinutes": 30,
        "expirationDateTime": "2025-07-07T15:30:00-07:00",
        "customTrayTimes": {
            "30 minutes": 1800,
            "1 hour": 3600,
            "2 hours": 7200
        }
    },
    "name": "Awake",
    "version": "1.0"
}
```

### Mode Values
- `0`: PASSIVE
- `1`: INDEFINITE  
- `2`: TIMED
- `3`: EXPIRABLE

## Examples

### Basic Usage

```powershell
# Keep computer awake indefinitely with display on
Awake.exe

# Keep computer awake for 1 hour (3600 seconds)
Awake.exe -t 3600

# Keep computer awake for 30 minutes without display
Awake.exe -t 1800 -d false

# Keep computer awake until specific time
Awake.exe -e "2025-07-07T17:00:00"

# Use PowerToys configuration
Awake.exe --use-pt-config
```

### Process Binding

```powershell
# Bind to specific process ID
Awake.exe -p 1234

# Bind to parent process
Awake.exe -u

# Bind to PowerToys runner with display control
Awake.exe -p 5678 -d true
```

### Advanced Scenarios

```powershell
# Long-running development session (8 hours)
Awake.exe -t 28800 -d false

# Presentation mode (keep display on indefinitely)
Awake.exe -t 0 -d true

# Overnight process (expire at 8 AM next day)
Awake.exe -e "2025-07-08T08:00:00"

# Bind to Visual Studio process
$vsProcess = Get-Process "devenv" | Select-Object -First 1
Awake.exe -p $vsProcess.Id
```

## Advanced Usage

### Custom Tray Times

Configure custom time shortcuts in PowerToys settings:

```json
"customTrayTimes": {
    "Quick break": 900,      // 15 minutes
    "Lunch break": 3600,     // 1 hour  
    "Meeting": 5400,         // 1.5 hours
    "Long task": 14400       // 4 hours
}
```

### Integration with Scripts

```powershell
# PowerShell script to start/stop Awake
function Start-AwakeSession {
    param(
        [int]$Hours = 0,
        [int]$Minutes = 30,
        [bool]$KeepDisplay = $true
    )
    
    $seconds = ($Hours * 3600) + ($Minutes * 60)
    $displayArg = if ($KeepDisplay) { "true" } else { "false" }
    
    Start-Process "Awake.exe" -ArgumentList "-t", $seconds, "-d", $displayArg
}

# Usage
Start-AwakeSession -Hours 2 -Minutes 15 -KeepDisplay $false
```

### Batch File Examples

```batch
@echo off
REM Quick 30-minute session
"C:\Program Files\PowerToys\Awake.exe" -t 1800

REM All-day work session until 6 PM
"C:\Program Files\PowerToys\Awake.exe" -e "2025-07-07T18:00:00"

REM Bind to current PowerShell session
for /f "tokens=2" %%i in ('tasklist /fi "imagename eq powershell.exe" /fo csv ^| findstr /v "PID"') do (
    "C:\Program Files\PowerToys\Awake.exe" -p %%i
    goto :done
)
:done
```

## Troubleshooting

### Common Issues

#### 1. Awake Already Running
**Error**: "PowerToys.Awake is already running! Exiting the application."  
**Solution**: Only one instance can run at a time. Close existing instance first.

#### 2. Invalid Time Limit
**Error**: Time limit parsing error  
**Solution**: Ensure value is between 0 and 4,294,967,295 seconds.

#### 3. Invalid PID
**Error**: PID parsing error  
**Solution**: Verify the process ID exists and is valid.

#### 4. Invalid Date Format
**Error**: Date/time parsing error  
**Solution**: Use ISO 8601 format: "YYYY-MM-DDTHH:MM:SS"

#### 5. Group Policy Restrictions
**Error**: "Group policy setting disables the tool"  
**Solution**: Contact system administrator to enable PowerToys Awake.

### Debug Information

Awake logs information to:
- **Location**: `%LOCALAPPDATA%\Microsoft\PowerToys\Awake\Logs`
- **File Format**: Timestamped log files
- **Content**: Startup, mode changes, errors, exit events

### System Requirements

- **OS**: Windows 10 version 2004 (build 19041) or later
- **Architecture**: x64, ARM64
- **Dependencies**: .NET runtime (included with PowerToys)
- **Permissions**: Standard user (no administrator required for basic functionality)

## Technical Details

### Power Management

Awake uses Windows Power Management APIs:
- **SetThreadExecutionState**: Prevents system sleep
- **ES_CONTINUOUS**: Maintains execution state
- **ES_SYSTEM_REQUIRED**: Prevents system sleep
- **ES_DISPLAY_REQUIRED**: Prevents display sleep

### System Integration

- **Mutex**: `PowerToys.Awake` prevents multiple instances
- **Event Handling**: `AwakeExitEvent` for clean shutdown
- **Tray Icons**: Different icons for each mode
- **File Watcher**: Monitors configuration file changes

### Exit Conditions

Awake terminates when:
1. Manual exit via tray menu or Ctrl+C
2. Time limit reached (timed mode)
3. Expiration time reached (expirable mode)
4. Bound process terminates (PID binding)
5. PowerToys shutdown signal
6. System shutdown/restart

### Performance Impact

- **CPU Usage**: Minimal (~0% when idle)
- **Memory Usage**: ~10-20 MB
- **Battery Impact**: Prevents sleep-related power savings
- **Network**: No network activity required

## Version Information

- **Current Build**: TILLSON_11272024
- **Assembly Version**: Retrieved at runtime
- **PowerToys Integration**: Full integration with PowerToys settings
- **Telemetry**: Basic usage telemetry (configurable in PowerToys settings)

---

*For the latest updates and documentation, visit the [PowerToys GitHub repository](https://github.com/microsoft/PowerToys).*
