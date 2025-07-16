# PowerToys Feature Specification: Hotkey Conflict Detection System

## 1. Executive Summary

PowerToys currently allows users to assign hotkeys across various modules without conflict detection mechanisms. This leads to overlapping hotkey assignments that can cause feature activation failures or ambiguous behavior. This specification defines a comprehensive hotkey conflict detection system that provides real-time feedback and prevents conflicting hotkey configurations, enhancing the overall reliability and user experience of PowerToys.

**Key Benefits:**
- Prevent hotkey conflicts between PowerToys modules and with system-reserved hotkeys
- Provide real-time feedback to users during hotkey configuration
- Offer centralized conflict management through a unified interface
- Maintain backwards compatibility with existing PowerToys installations

## 2. Requirements

#### Core Conflict Detection
- **Registration-time validation**: When a module attempts to register a hotkey, the system must validate against existing assignments and system-reserved hotkeys
- **Real-time UI feedback**: When users modify hotkeys in the Settings UI, immediate conflict checking with visual indicators
- **Cross-module awareness**: Detection of conflicts between different PowerToys modules

#### Conflict Classification
- **In-app conflicts**: Multiple PowerToys modules assigned the same hotkey combination
- **System conflicts**: Hotkeys reserved or registered by Windows or other applications
- **Granular identification**: Each conflict includes specific module names and hotkey identifiers

#### Data Management
- **Unique hotkey identification**: Every hotkey tagged with module name and descriptive identifier
- **Configuration persistence**: Conflict state maintained across application restarts
- **Backwards compatibility**: Automatic upgrade of existing configurations

## 3. Architecture Overview

### 3.1 System Initialization Workflow
<div align="center">
<img src="../images/hotkeyConflict/InitStageWorkFLow.png" alt="System initialization and hotkey registration flow during PowerToys startup" width="600px">
</div>

**PowerToys startup and hotkey conflict detection initialization:**

1. **PowerToys Runner Startup**
   - Runner process initializes and creates HotkeyConflictManager singleton instance
   - Initializes internal data structures for hotkey storage and conflict detection

2. **Module Registration Phase**
   - Each PowerToys module starts up and register hotkeys
   - Registration includes:
     - HotkeyConfig structure with key, modifiers, hotkeyName, ModuleName
     - Automatic conflict detection during registration process
   - HotkeyConflictManager updates internal registry map

3. **Conflict Detection During Startup**
   - For each hotkey registration, the manager checks:
     - Duplicate hotkey combinations (in-app conflicts)
     - System hotkey registry via Windows API calls (system conflicts)
   - Conflicts are detected but don't block module initialization

### 3.2 Module Settings Page Real-time Conflict Detection
<div align="center">
<img src="../images/hotkeyConflict/ModuleSettingsPageUIDisplay.png" alt="Real-time conflict detection UI flow in module settings page" width="800px">
</div>

**Interactive conflict detection during user hotkey configuration:**

1. **User Input Capture**
   - User modifies hotkey in Settings UI ShortcutControl component
   - HotkeyHelper captures key combination and converts to internal format

2. **IPC Conflict Check Request**
   - Settings UI sends `check_hotkey_conflict` IPC message to Runner
   - Request includes:
     - Hotkey combination (key code + modifier flags)
     - Module name and hotkey identifier
     - Unique request ID for response correlation

3. **Runner-side Conflict Processing**
   - HotkeyConflictManager receives IPC request via `ReceiveJsonMessage`
   - Calls `HotkeyConflictManager::HasConflict()` method to:
     - Check against currently registered hotkeys
     - Validate against system-reserved combinations
   - Prepares JSON response with conflict status and details

4. **UI Response and Visual Feedback**
   - **Conflict detected:**
     - UI update
     - Tooltip displays conflict details (module name + hotkey name)
   - **No conflict:**
     - UI shows normal state

### 3.3 Global Conflict Overview Dashboard (ShortcutConflictWindow)
<div align="center">
<img src="../images/hotkeyConflict/AllConflictsDisplayPage.png" alt="Comprehensive conflict summary displayed on general settings page" width="580px">
</div>

**System-wide conflict management interface:**

1. **Complete Conflict Data Retrieval**
   - Settings UI sends `get_all_hotkey_conflicts` IPC message to Runner
   - Runner responds with comprehensive conflict data including all in-app and system conflicts

2. **Structured Conflict Display**
   - **In-App Conflicts Section:**
     - Shows conflicts between PowerToys modules
   
   - **System Conflicts Section:**
     - Lists PowerToys hotkeys that conflict with Windows system shortcuts
     - Shows affected module and specific hotkey function

3. **Integrated Resolution Tools**
   - Direct hotkey editing within conflict management interface
   - Navigation links to affected module settings pages

### 3.4 Core Components Overview

The hotkey conflict detection system consists of four main components that work together to provide comprehensive conflict management:

#### 3.4.1 HotkeyConflictManager (C++ - Runner Process)
**Purpose**: Centralized singleton managing all hotkey registrations and conflict detection

**Key Responsibilities**:
- Maintain registry of all registered hotkeys with module ownership
- Detect conflicts during hotkey registration/modification
- Provide conflict query APIs for Settings UI

**Data Structures**:
```cpp
class HotkeyConflictManager {
private:
    // Successfully registered hotkeys (no conflicts)
    std::unordered_map<uint16_t, HotkeyConflictInfo> hotkeyMap;
    
    // System-level conflicts  
    std::unordered_map<uint16_t, std::unordered_set<HotkeyConflictInfo>> sysConflictHotkeyMap;
    
    // In-app module conflicts
    std::unordered_map<uint16_t, std::unordered_set<HotkeyConflictInfo>> inAppConflictHotkeyMap;
    
    // Disabled module hotkeys
    std::unordered_map<std::wstring, std::vector<HotkeyConflictInfo>> disabledHotkeys;
};
```

#### 3.4.2. IPC Communication Protocol (Runner ↔ Settings UI)
**Purpose**: Enable real-time conflict checking between Runner and Settings UI

**Key Message Types**:
- `check_hotkey_conflict`: Single hotkey validation
- `get_all_hotkey_conflicts`: Query all the hotkey conflict data from runner
- Response correlation via unique request IDs

#### 3.4.3. Settings UI Integration (C# - Settings Process)
**Purpose**: Provide user interface for conflict detection and resolution

**Key Components**:
- `HotkeyConflictHelper`: Handles IPC communication for conflict checking
- `ShortcutControl`: Enhanced hotkey input with visual conflict indicators
- `ShortcutConflictViewModel`: Centralized conflict management interface

#### 3.4.4. Module Integration Layer
**Purpose**: Seamless integration with existing PowerToys modules

**Requirements**:
- Modules provide unique hotkey names and the module name
- Registration/unregistration via centralized manager


## 4. Technical Implementation

### 4.1 Hotkey Representation and Identification

#### 4.1.1 Enhanced PowertoyModuleIface Integration

```cpp
class PowertoyModuleIface {
public:
    struct Hotkey {
        bool win = false;
        bool ctrl = false;
        bool shift = false;
        bool alt = false;
        unsigned char key = 0;
        const wchar_t* name = nullptr;  // Unique identifier within module

        std::strong_ordering operator<=>(const Hotkey&) const = default;
    };

    struct HotkeyEx {
        WORD modifiersMask = 0;
        WORD vkCode = 0;
        const wchar_t* name = nullptr;  // Unique identifier within module
    };
};
```

**Unique Identification Strategy**:
- **Module-scoped uniqueness**: Hotkey names must be unique within each module
- **Global identification**: Combination of module name + hotkey name provides global uniqueness

### 4.2 Core Conflict Detection Algorithm

#### 4.2.1 Detection Logic Flow
```cpp
HotkeyConflictType HotkeyConflictManager::HasConflict(Hotkey const& hotkey, 
                                                      const wchar_t* moduleName, 
                                                      const wchar_t* hotkeyName) {
    uint16_t handle = GetHotkeyHandle(hotkey);
    
    // Check if module is disabled
    if (IsModuleDisabled(moduleName)) {
        return HotkeyConflictType::NoConflict;
    }
    
    // Check for system-level conflicts using Windows API
    if (HasConflictWithSystemHotkey(hotkey)) {
        return HotkeyConflictType::SystemConflict;
    }
    
    // Check for in-app conflicts with other modules
    auto it = hotkeyMap.find(handle);
    if (it != hotkeyMap.end()) {
        // Check if it's the same module/hotkey (not a conflict)
        if (it->second.moduleName == moduleName && it->second.hotkeyName == hotkeyName) {
            return HotkeyConflictType::NoConflict;
        }
        return HotkeyConflictType::InAppConflict;
    }
    
    return HotkeyConflictType::NoConflict;
}
```

#### 4.2.2 System Conflict Detection
```cpp
bool HotkeyConflictManager::HasConflictWithSystemHotkey(const Hotkey& hotkey) {
    static int hotkeyId = 10000;  // Use unique ID range
    
    UINT modifiers = 0;
    if (hotkey.win) modifiers |= MOD_WIN;
    if (hotkey.ctrl) modifiers |= MOD_CONTROL;
    if (hotkey.shift) modifiers |= MOD_SHIFT;
    if (hotkey.alt) modifiers |= MOD_ALT;
    
    // Attempt registration with Windows
    if (!RegisterHotKey(nullptr, hotkeyId++, modifiers, hotkey.key)) {
        if (GetLastError() == ERROR_HOTKEY_ALREADY_REGISTERED) {
            return true;  // Conflict detected
        }
    } else {
        // Registration succeeded - unregister immediately
        UnregisterHotKey(nullptr, hotkeyId - 1);
    }
    
    return false;  // No conflict
}
```


### 4.3 IPC Communication Protocol

#### 4.3.1 Key component descriptions:
- **HotkeyConflictHelper**: responsible for sending single hotkey conflict check requests

- **GlobalHotkeyConflictManager**: responsible for sending requests to get all conflicts

- **IPCResponseService**: responsible for handling conflict check responses returned by runner

#### 4.3.2 Message Format Specification

**Single Hotkey Conflict Check**:
```json
// Request
{
  "check_hotkey_conflict": {
    "request_id": "generated-uuid",
    "win": true,
    "ctrl": false,
    "shift": true,
    "alt": false,
    "key": 86,
    "moduleName": "AdvancedPaste",
    "hotkeyName": "PasteAsPlainTextShortcut"
  }
}

// Response
{
  "response_type": "hotkey_conflict_result",
  "request_id": "matching-uuid",
  "has_conflict": true,
  "all_conflicts": [
    {
      "module": "FancyZones",
      "hotkey_name": "EditorHotkey"
    }
  ]
}
```

**Global Conflicts Query**:
```json
// Request
{
  "get_all_hotkey_conflicts": {}
}

// Response
{
  "response_type": "all_hotkey_conflicts",
  "inAppConflicts": [
    {
      "hotkey": {"win": true, "ctrl": false, "shift": true, "alt": false, "key": 86},
      "modules": [
        {"moduleName": "AdvancedPaste", "hotkeyName": "PasteAsPlainTextShortcut"},
        {"moduleName": "FancyZones", "hotkeyName": "EditorHotkey"}
      ]
    }
  ],
  "sysConflicts": [
    {
      "hotkey": {"win": true, "ctrl": true, "shift": false, "alt": false, "key": 67},
      "modules": [
        {"moduleName": "ColorPicker", "hotkeyName": "ActivationShortcut"}
      ]
    }
  ]
}
```

---

## 5. User Interface Design

### 5.1 Settings UI Integration

#### 5.1.1 Real-time Conflict Feedback
- **Visual indicators**: Warning icons and tooltips in hotkey input controls
- **Conflict details**: Clear identification of conflicting modules and functions

#### 5.1.2 Enhanced ShortcutControl Component
```xml
<controls:ShortcutControl
    HotkeySettings="{x:Bind HotkeySettings, Mode=TwoWay}"
    HasConflict="{x:Bind HasConflict, Mode=OneWay}"
    ConflictTooltip="{x:Bind ConflictDetails, Mode=OneWay}"
    IsEnabled="True" />
```

### 5.2 Centralized Conflict Management Window

**Purpose**: Provide unified interface for viewing and resolving all hotkey conflicts

**Key Features**:
- **Centralized display**: All module conflicts in single window
- **In-place editing**: Direct hotkey modification without navigation
- **Real-time updates**: Live conflict status as modifications are made

**Design Patterns Used**:
- **Proxy Pattern**: Unified access to all module ViewModels
- **Observer Pattern**: Two-way binding for real-time synchronization
- **Factory Pattern**: Lazy-loaded module ViewModel creation

#### 5.2.1 Core Architecture Implementation

**Data Flow Architecture**:
```
Runner (C++) → IPC Messages → GlobalHotkeyConflictManager → ShortcutConflictViewModel → UI Display
                ↑                                                    ↓
      Conflict Detection Results                             User Modifies Hotkeys
                ↑                                                    ↓
       HotkeyConflictDetector                                 Module ViewModels
```

**ViewModel Class Structure**:
```csharp
public class ShortcutConflictViewModel : PageViewModelBase
{
    // Core data collections
    private ObservableCollection<HotkeyConflictGroupData> conflictItems;
    private readonly Dictionary<string, Func<PageViewModelBase>> viewModelFactories;
    private readonly Dictionary<string, PageViewModelBase> moduleViewModels;
    
    // Conflict management
    private AllHotkeyConflictsData conflictsData;
    private readonly Dictionary<string, HotkeySettings> originalSettings;
    
    // Services
    private readonly GlobalHotkeyConflictManager globalConflictManager;
    private readonly INavigationHelper navigationHelper;
    
    // Conflict data shown on ShortcutConflictWindow
    public ObservableCollection<HotkeyConflictGroupData> ConflictItems
    {
        get => conflictItems;
        private set => SetProperty(ref conflictItems, value);
    }
}
```

**Conflict Data Structure**:
```csharp
public class HotkeyConflictGroupData : INotifyPropertyChanged
{
    public HotkeySettings Hotkey { get; set; }           // Conflicting hotkey combination
    public List<ModuleHotkeyData> Modules { get; set; }  // Modules using this hotkey
    public bool IsSystemConflict { get; set; }          // System vs in-app conflict
    public string DisplayText { get; set; }             // Human-readable hotkey string
    public int ConflictCount => Modules?.Count ?? 0;    // Number of conflicting modules
}

public class ModuleHotkeyData : INotifyPropertyChanged
{
    private string _moduleName;
    private string _hotkeyName;
    private HotkeySettings _hotkeySettings;             // Supports two-way binding
    private bool _isSystemConflict;
}
```

#### 5.2.2 Proxy Pattern Implementation

**Module ViewModel Factory System**:
```csharp
private void InitializeViewModelFactories()
{
    viewModelFactories["advancedpaste"] = () => new AdvancedPasteViewModel(settingsUtils, ipcMSGCallBackManager);
    viewModelFactories["alwaysontop"] = () => new AlwaysOnTopViewModel(settingsUtils, ipcMSGCallBackManager);
    viewModelFactories["awake"] = () => new AwakeViewModel(settingsUtils, ipcMSGCallBackManager);
    viewModelFactories["colorpicker"] = () => new ColorPickerViewModel(settingsUtils, ipcMSGCallBackManager);
    viewModelFactories["fancyzones"] = () => new FancyZonesViewModel(settingsUtils, ipcMSGCallBackManager);
    // ... additional modules
}

private PageViewModelBase GetOrCreateViewModel(string moduleKey)
{
    if (!moduleViewModels.TryGetValue(moduleKey, out var viewModel))
    {
        if (viewModelFactories.TryGetValue(moduleKey, out var factory))
        {
            viewModel = factory(); // Lazy creation only when needed
            moduleViewModels[moduleKey] = viewModel;
        }
    }
    return viewModel;
}
```

**Cross-Module Hotkey Access**:
```csharp
private HotkeySettings GetHotkeySettingsFromViewModel(string moduleName, string hotkeyName)
{
    var moduleKey = GetModuleKey(moduleName);
    var viewModel = GetOrCreateViewModel(moduleKey);
    
    return moduleKey switch
    {
        "advancedpaste" => GetAdvancedPasteHotkeySettings(viewModel as AdvancedPasteViewModel, hotkeyName),
        "alwaysontop" => GetAlwaysOnTopHotkeySettings(viewModel as AlwaysOnTopViewModel, hotkeyName),
        "colorpicker" => GetColorPickerHotkeySettings(viewModel as ColorPickerViewModel, hotkeyName),
        "fancyzones" => GetFancyZonesHotkeySettings(viewModel as FancyZonesViewModel, hotkeyName),
        "mouseutils" => GetMouseUtilsHotkeySettings(viewModel as MouseUtilsViewModel, hotkeyName),
        _ => null,
    };
}
```

#### 5.2.3 Observer Pattern Implementation

**Two-Way Binding Synchronization**:
```csharp
private void OnModuleHotkeyDataPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (sender is ModuleHotkeyData moduleData && e.PropertyName == nameof(ModuleHotkeyData.HotkeySettings))
    {
        // Synchronize changes back to original module ViewModel
        UpdateModuleViewModelHotkeySettings(moduleData.ModuleName, moduleData.HotkeyName, moduleData.HotkeySettings);
        
        // Trigger real-time conflict re-checking
        RefreshConflictsAsync();
    }
}

private void UpdateModuleViewModelHotkeySettings(string moduleName, string hotkeyName, HotkeySettings newHotkeySettings)
{
    var moduleKey = GetModuleKey(moduleName);
    var viewModel = GetOrCreateViewModel(moduleKey);
    
    switch (moduleKey)
    {
        case "advancedpaste":
            UpdateAdvancedPasteHotkeySettings(viewModel as AdvancedPasteViewModel, hotkeyName, newHotkeySettings);
            break;
        case "fancyzones":
            UpdateFancyZonesHotkeySettings(viewModel as FancyZonesViewModel, hotkeyName, newHotkeySettings);
            break;
        // ... additional module cases
    }
}
```

#### 5.2.4 Factory Pattern Implementation

**Lazy-Loading Strategy**:
```csharp
public class ViewModelFactory
{
    private readonly ISettingsUtils settingsUtils;
    private readonly IpcMSGCallBackManager ipcManager;
    
    public ViewModelFactory(ISettingsUtils settingsUtils, IpcMSGCallBackManager ipcManager)
    {
        this.settingsUtils = settingsUtils;
        this.ipcManager = ipcManager;
    }
    
    public PageViewModelBase CreateViewModel(string moduleKey)
    {
        return moduleKey switch
        {
            "advancedpaste" => new AdvancedPasteViewModel(settingsUtils, ipcManager),
            "fancyzones" => new FancyZonesViewModel(settingsUtils, ipcManager),
            "colorpicker" => new ColorPickerViewModel(settingsUtils, ipcManager),
            // ... other modules
            _ => throw new ArgumentException($"Unknown module: {moduleKey}")
        };
    }
}
```

#### 5.2.5 Performance Optimizations

**Memory Management**:
```csharp
public override void Dispose()
{
    // Unsubscribe from all property change events
    foreach (var conflictGroup in ConflictItems)
    {
        foreach (var module in conflictGroup.Modules)
        {
            module.PropertyChanged -= OnModuleHotkeyDataPropertyChanged;
        }
    }
    
    // Dispose all created module ViewModels
    foreach (var viewModel in moduleViewModels.Values)
    {
        viewModel?.Dispose();
    }
    
    moduleViewModels.Clear();
    originalSettings.Clear();
    
    base.Dispose();
}
```

**Change Detection Optimization**:
```csharp
private bool AreHotkeySettingsEqual(HotkeySettings settings1, HotkeySettings settings2)
{
    if (ReferenceEquals(settings1, settings2)) return true;
    if (settings1 == null || settings2 == null) return false;
    
    return settings1.Win == settings2.Win &&
           settings1.Ctrl == settings2.Ctrl &&
           settings1.Alt == settings2.Alt &&
           settings1.Shift == settings2.Shift &&
           settings1.Code == settings2.Code;
}
```

#### 5.2.6 Conflict Resolution Workflow
1. User opens centralized conflict management window
2. System displays all current conflicts grouped by hotkey combination
3. User can modify conflicting hotkeys directly in the interface
4. Changes are immediately synchronized to respective module ViewModels
5. Conflict status updates in real-time as changes are made

---

## 6. Other Data Flow and Workflows

#### Hotkey Registration During Startup Phase
During the PowerToys startup phase, the runner loads all modules one by one and obtains the module's hotkeys through the `get_hotkeys()` interface or `GetHotkeyEx()` interface in the module interface, then registers the hotkeys through the `UpdateHotkeyEx` or `update_hotkeys` methods.

```cpp
void PowertoyModule::update_hotkeys()
{
    CentralizedKeyboardHook::ClearModuleHotkeys(pt_module->get_key());

    size_t hotkeyCount = pt_module->get_hotkeys(nullptr, 0);
    std::vector<PowertoyModuleIface::Hotkey> hotkeys(hotkeyCount);
    pt_module->get_hotkeys(hotkeys.data(), hotkeyCount);

    auto modulePtr = pt_module.get();

    for (size_t i = 0; i < hotkeyCount; i++)
    {
        CentralizedKeyboardHook::SetHotkeyAction(pt_module->get_key(), hotkeys[i], [modulePtr, i] {
            Logger::trace(L"{} hotkey is invoked from Centralized keyboard hook", modulePtr->get_key());
            return modulePtr->on_hotkey(i);
        });
    }
}

void PowertoyModule::UpdateHotkeyEx()
{
    CentralizedHotkeys::UnregisterHotkeysForModule(pt_module->get_key());
    auto container = pt_module->GetHotkeyEx();
    if (container.has_value() && pt_module->is_enabled())
    {
        auto hotkey = container.value();
        auto modulePtr = pt_module.get();
        auto action = [modulePtr](WORD /*modifiersMask*/, WORD /*vkCode*/) {
            Logger::trace(L"{} hotkey Ex is invoked from Centralized keyboard hook", modulePtr->get_key());
            modulePtr->OnHotkeyEx();
        };

        CentralizedHotkeys::AddHotkeyAction({ hotkey.modifiersMask, hotkey.vkCode }, { pt_module->get_key(), action });
    }

    if (pt_module->keep_track_of_pressed_win_key())
    {
        auto modulePtr = pt_module.get();
        auto action = [modulePtr] {
            modulePtr->OnHotkeyEx();
            return false;
        };
        CentralizedKeyboardHook::AddPressedKeyAction(pt_module->get_key(), VK_LWIN, pt_module->milliseconds_win_key_must_be_pressed(), action);
        CentralizedKeyboardHook::AddPressedKeyAction(pt_module->get_key(), VK_RWIN, pt_module->milliseconds_win_key_must_be_pressed(), action);
    }
}
```

The actual functions that perform hotkey registration are `CentralizedHotkeys::AddHotkeyAction` and `CentralizedKeyboardHook::SetHotkeyAction`

```cpp
bool AddHotkeyAction(Shortcut shortcut, Action action)
{
    if (!actions[shortcut].empty())
    {
        // It will only work if previous one is rewritten
        Logger::warn(L"{} shortcut is already registered", ToWstring(shortcut));
    }

    actions[shortcut].push_back(action);
    // Register hotkey if it is the first shortcut
    if (actions[shortcut].size() == 1)
    {
        if (ids.find(shortcut) == ids.end())
        {
            static int nextId = 0;
            ids[shortcut] = nextId++;
        }

        if (!RegisterHotKey(runnerWindow, ids[shortcut], shortcut.modifiersMask, shortcut.vkCode))
        {
            Logger::warn(L"Failed to add {} shortcut. {}", ToWstring(shortcut), get_last_error_or_default(GetLastError()));
            return false;
        }
        Logger::trace(L"{} shortcut registered", ToWstring(shortcut));
        return true;
    }
    return true;
}

void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept
{
    Logger::trace(L"Register hotkey action for {}", moduleName);
    std::unique_lock lock{ mutex };
    hotkeyDescriptors.insert({ .hotkey = hotkey, .moduleName = moduleName, .action = std::move(action) });
}
```
Similarly, hotkey unregistration occurs in the `CentralizedHotkeys::UnregisterHotkeysForModule` and `CentralizedKeyboardHook::ClearModuleHotkeys` methods

```cpp
void ClearModuleHotkeys(const std::wstring& moduleName) noexcept
{
    Logger::trace(L"UnRegister hotkey action for {}", moduleName);
    {
        std::unique_lock lock{ mutex };
        auto it = hotkeyDescriptors.begin();
        while (it != hotkeyDescriptors.end())
        {
            if (it->moduleName == moduleName)
            {
                it = hotkeyDescriptors.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }
    {
        std::unique_lock lock{ pressedKeyMutex };
        auto it = pressedKeyDescriptors.begin();
        while (it != pressedKeyDescriptors.end())
        {
            if (it->moduleName == moduleName)
            {
                it = pressedKeyDescriptors.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }
}

void UnregisterHotkeysForModule(std::wstring moduleName)
{
    for (auto it = actions.begin(); it != actions.end(); it++)
    {
        auto val = std::find_if(it->second.begin(), it->second.end(), [moduleName](Action a) { return a.moduleName == moduleName; });
        if (val != it->second.end())
        {
            it->second.erase(val);
            if (it->second.empty())
            {
                if (!UnregisterHotKey(runnerWindow, ids[it->first]))
                {
                    Logger::warn(L"Failed to unregister {} shortcut. {}", ToWstring(it->first), get_last_error_or_default(GetLastError()));
                }
                else
                {
                    Logger::trace(L"{} shortcut unregistered", ToWstring(it->first));
                }
            }
        }
    }
}
```
Therefore, **hotkey conflict detection logic needs to be added to these four functions.**

### Module Registration and Hotkey Management

#### Integration with Existing PowerToys Infrastructure

**PowerToys Runner Integration**:
```cpp
void PowertoyModule::UpdateHotkey()
{
    // Unregister existing hotkeys
    CentralizedKeyboardHook::UnregisterHotkeysForModule(pt_module->get_key());
    HotkeyConflictManager::GetInstance().UnregisterModule(pt_module->get_key());
    
    auto hotkeys = pt_module->get_hotkeys();
    if (hotkeys.has_value() && pt_module->is_enabled())
    {
        auto& hotkeyArray = hotkeys.value();
        auto modulePtr = pt_module.get();
        
        for (size_t i = 0; i < hotkeyArray.size(); i++)
        {
            // Register with conflict manager before system registration
            HotkeyConflictManager::GetInstance().RegisterHotkey(
                pt_module->get_key(), 
                hotkeyArray[i].name, 
                hotkeyArray[i]
            );
            
            CentralizedKeyboardHook::SetHotkeyAction(pt_module->get_key(), hotkeyArray[i], 
                [modulePtr, i] {
                    Logger::trace(L"{} hotkey invoked", modulePtr->get_key());
                    return modulePtr->on_hotkey(i);
                });
        }
    }
}
```

**Conflict Detection Integration Points**:
1. `CentralizedHotkeys::AddHotkeyAction` - System-level hotkey registration
2. `CentralizedKeyboardHook::SetHotkeyAction` - Module hotkey registration  
3. `UnregisterHotkeysForModule` - Cleanup during module disable/unload
4. `ClearModuleHotkeys` - Complete module hotkey removal

### Advanced UI Components

#### Enhanced ShortcutConflictWindow

**Core Features**:
- **Visual Conflict Display**: KeyVisual controls for hotkey combinations
- **Module Grouping**: Conflicts organized by hotkey with module identification
- **Real-time Editing**: In-place hotkey modification with two-way binding
- **Quick Navigation**: Direct links to individual module settings

**Navigation Logic**:
```csharp
private void SettingsCard_Click(object sender, RoutedEventArgs e)
{
    if (ModuleNavigationHelper.NavigateToModulePage(moduleName))
    {
        this.Close(); // Close conflict window after navigation
    }
}
```

#### Centralized Conflict Management Implementation

**MVVM Architecture**:
```csharp
// ViewModel handles data logic and binding
public ShortcutConflictViewModel ViewModel { get; private set; }

// UI data binding
ItemsSource="{x:Bind ViewModel.ConflictItems, Mode=OneWay}"
```

**Dynamic UI Generation**:
- **Data Templates**: Automatically generate UI for conflict groups
- **Conditional Display**: System conflict warnings shown only when applicable
- **Localization Support**: Multilingual hotkey descriptions and error messages

```xml
<tkcontrols:SettingsCard Visibility="{x:Bind IsSystemConflict}">
    <TextBlock x:Uid="ShortcutConflictWindow_SystemShortcutMessage" />
</tkcontrols:SettingsCard>
```

**Localization Implementation**:
```csharp
// Set localization delegate for custom actions
LocalizationHelper.GetCustomActionNameDelegate = GetCustomActionName;

// Generate localized hotkey headers
card.Header = LocalizationHelper.GetLocalizedHotkeyHeader(
    moduleData.ModuleName, 
    moduleData.HotkeyName);
```

### User Interaction Workflows

#### Complete Conflict Resolution Flow
1. **Conflict Detection**: `IPC Messages → ViewModel Update → UI Refresh`
2. **User Modification**: `ShortcutControl Edit → TwoWay Binding → ViewModel Update → IPC Message → Runner Updates`
3. **Module Navigation**: `Click Card → Extract Module Name → Navigation Helper → Close Conflict Window`

---

## 7. Implementation Details

### Backend Implementation (C++)

#### HotkeyConflictManager Core Implementation

**Singleton Pattern Design**:
```cpp
class HotkeyConflictManager {
private:
    static std::unique_ptr<HotkeyConflictManager> instance;
    static std::mutex instanceMutex;
    
    // Internal data structures
    std::unordered_map<uint16_t, HotkeyInfo> hotkeyMap;
    std::set<std::wstring> disabledModules;
    mutable std::shared_mutex dataMutex;
    
    struct HotkeyInfo {
        std::wstring moduleName;
        std::wstring hotkeyName;
        Hotkey hotkey;
        std::chrono::system_clock::time_point registrationTime;
    };
    
public:
    static HotkeyConflictManager& GetInstance();
    
    // Core functionality
    HotkeyConflictType HasConflict(const Hotkey& hotkey, 
                                  const wchar_t* moduleName, 
                                  const wchar_t* hotkeyName);
    
    void RegisterHotkey(const wchar_t* moduleName, 
                       const wchar_t* hotkeyName, 
                       const Hotkey& hotkey);
    
    void UnregisterHotkey(const wchar_t* moduleName, 
                         const wchar_t* hotkeyName);
    
    std::vector<ConflictInfo> GetAllConflicts() const;
    
    // Module management
    void SetModuleEnabled(const wchar_t* moduleName, bool enabled);
    bool IsModuleEnabled(const wchar_t* moduleName) const;
};
```

**Thread-safe Operations**:
```cpp
void HotkeyConflictManager::RegisterHotkey(const wchar_t* moduleName, 
                                          const wchar_t* hotkeyName, 
                                          const Hotkey& hotkey) {
    std::unique_lock<std::shared_mutex> lock(dataMutex);
    
    uint16_t handle = GetHotkeyHandle(hotkey);
    
    HotkeyInfo info;
    info.moduleName = moduleName;
    info.hotkeyName = hotkeyName;
    info.hotkey = hotkey;
    info.registrationTime = std::chrono::system_clock::now();
    
    hotkeyMap[handle] = std::move(info);
}

std::vector<ConflictInfo> HotkeyConflictManager::GetAllConflicts() const {
    std::shared_lock<std::shared_mutex> lock(dataMutex);
    
    std::vector<ConflictInfo> conflicts;
    std::map<uint16_t, std::vector<HotkeyInfo>> hotkeyGroups;
    
    // Group hotkeys by handle
    for (const auto& [handle, info] : hotkeyMap) {
        if (!IsModuleEnabled(info.moduleName.c_str())) continue;
        hotkeyGroups[handle].push_back(info);
    }
    
    // Find conflicts (groups with > 1 hotkey)
    for (const auto& [handle, group] : hotkeyGroups) {
        if (group.size() > 1) {
            ConflictInfo conflict;
            conflict.hotkey = group[0].hotkey;
            conflict.conflictingModules = group;
            conflicts.push_back(conflict);
        }
    }
    
    return conflicts;
}
```

### Frontend Implementation (C#)

#### Centralized Conflict Management Window

**XAML Layout Design**:
```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    
    <!-- Header with conflict summary -->
    <StackPanel Grid.Row="0" Margin="20">
        <TextBlock Style="{StaticResource SubtitleTextBlockStyle}">
            Hotkey Conflict Management
        </TextBlock>
        <TextBlock Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}">
            <Run Text="{x:Bind ViewModel.TotalConflicts}"/>
            <Run Text="conflicts found across"/>
            <Run Text="{x:Bind ViewModel.AffectedModules}"/>
            <Run Text="modules"/>
        </TextBlock>
    </StackPanel>
    
    <!-- Main conflict list -->
    <ListView Grid.Row="1" 
              ItemsSource="{x:Bind ViewModel.ConflictGroups}"
              SelectionMode="None">
        <ListView.ItemTemplate>
            <DataTemplate x:DataType="local:ConflictGroup">
                <Expander HorizontalAlignment="Stretch">
                    <Expander.Header>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            
                            <TextBlock Grid.Column="0">
                                <Run Text="{x:Bind HotkeyDisplayText}"/>
                                <Run Text="(" Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                                <Run Text="{x:Bind ConflictCount}" Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                                <Run Text="modules affected)" Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                            </TextBlock>
                            
                            <FontIcon Grid.Column="1" 
                                     Glyph="&#xE7BA;" 
                                     Foreground="{ThemeResource SystemFillColorCriticalBrush}"/>
                        </Grid>
                    </Expander.Header>
                    
                    <ItemsRepeater ItemsSource="{x:Bind ConflictingModules}">
                        <ItemsRepeater.ItemTemplate>
                            <DataTemplate x:DataType="local:ModuleHotkeyInfo">
                                <Grid Margin="10,5" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" 
                                      CornerRadius="4" Padding="15">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="200"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="{x:Bind ModuleName}" 
                                                  Style="{StaticResource BodyStrongTextBlockStyle}"/>
                                        <TextBlock Text="{x:Bind HotkeyDescription}" 
                                                  Foreground="{ThemeResource SystemControlForegroundBaseMediumBrush}"/>
                                    </StackPanel>
                                    
                                    <controls:ShortcutControl Grid.Column="1"
                                                            HotkeySettings="{x:Bind CurrentHotkey, Mode=TwoWay}"
                                                            Margin="10,0"/>
                                    
                                    <Button Grid.Column="2" 
                                           Content="Apply" 
                                           Command="{x:Bind UpdateHotkeyCommand}"
                                           IsEnabled="{x:Bind HasValidNewHotkey}"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsRepeater.ItemTemplate>
                    </ItemsRepeater>
                </Expander>
            </DataTemplate>
        </ListView.ItemTemplate>
    </ListView>
    
    <!-- Action buttons -->
    <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="20">
        <Button Content="Auto-resolve" Command="{x:Bind ViewModel.AutoResolveCommand}" Margin="0,0,10,0"/>
        <Button Content="Close" Command="{x:Bind ViewModel.CloseCommand}"/>
    </StackPanel>
</Grid>
```

---

## 8. Appendix

### Reference Implementation
- **Related Pull Request:** [PowerToys hotkey conflict detection](https://github.com/microsoft/PowerToys/pull/40457)

### Limitations
1. **System Conflict Detection:** Limited to Windows API capabilities
2. **Third-party Applications:** Cannot detect conflicts with external applications
4. **Only support the following modules**: AdvancedPaste,	AlwaysOnTop, ColorPicker,	CropAndLock, MeasureTool, Peek, PowerLauncher, PowerOCR, ShortcutGuide, Workspaces,	MouseHighlighter, MouseCrossHair, FindMyMouse, MouseJump, MouseWithoutBorders

### Migration Guide
For developers integrating with the new hotkey conflict detection system:

1. **Add hotkey names** to existing `Hotkey` and `HotkeyEx` structures
2. **Update registration calls** to include module and hotkey identifiers
3. **Implement conflict handlers** in module-specific ViewModels and `ShortcutConflictViewModel`
4. **Test thoroughly** with existing and new hotkey combinations
