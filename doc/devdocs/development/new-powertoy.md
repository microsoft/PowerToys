# 🧭 Creating a New PowerToy: End-to-End Developer Guide

First of all, thank you for wanting to contribute to PowerToys. The work we do would not be possible without the support of community supporters like you.

This guide documents the process of building a new PowerToys utility from scratch, including architecture decisions, integration steps, and common pitfalls.

---

## 1. Overview and Prerequisites

A PowerToy module is a self-contained utility integrated into the PowerToys ecosystem. It can be UI-based, service-based, or both.

### Requirements

* [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/) and the following workloads/individual components:
  - Desktop Development with C++
  - WinUI application development
  - .NET desktop development
  - Windows 10 SDK (10.0.22621.0)
  - Windows 11 SDK (10.0.26100.3916)
* .NET 8 SDK
* Clone the [PowerToys repository](https://github.com/microsoft/PowerToys/tree/main) locally
* [Validate that you are able to build and run](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/development/debugging.md) `PowerToys.slnx`.

Optional: 
* [WiX v5 toolset](https://github.com/microsoft/PowerToys/tree/main) for the installer

> Note: To ensure all the correct VS Workloads are installed, use [the WinGet configuration files](https://github.com/microsoft/PowerToys/tree/e13d6a78aafbcf32a4bb5f8581d041e1d057c3f1/.config) in the project repository. (Use the one that matches your VS distribution. ie: VS Community would use `configuration.winget`)

### Folder Structure

```
src/
  modules/
    your_module/
      YourModule.sln
      YourModuleInterface/
      YourModuleUI/ (if needed)
      YourModuleService/ (if needed)
```

---
## 2. Design and Planning

### Decide the Type of Module
Think about how your module works and which existing modules behave similarly. You are going to want to think about the UI needed for the application, the lifecycle, whether it is a service that is always running or event based. Below are some basic scenarios with some modules to explore. You can write your application in C++ or C#.
* **UI-only:** e.g., ColorPicker
* **Background service:** e.g., LightSwitch, Awake
* **Hybrid (UI + background logic):** e.g., ShortcutGuide
* **C++/C# interop:** e.g., PowerRename 

### Writing your Module Interface
Begin by setting up the [PowerToy module template project](https://github.com/microsoft/PowerToys/tree/main/tools/project_template). This will generate boilerplate for you to begin your new module. Below are the key headers in the Module Interface (`dllmain.cpp`) and an explanation of their purpose:
1. This is where module settings are defined. These can be anything from strings, bools, ints, and even custom Enums.
```c++
struct ModuleSettings {};
```

2. This is the header for the full class. It inherits the PowerToyModuleIface
```c++
class ModuleInterface : public PowertoyModuleIface 
{
  private:
    // the private members of the class
    // Can include the enabled variable, logic for event handlers, or hotkeys.
  public:
    // the public members of the class
    // Will include the constructor and initialization logic.
}
```

> Note: Many of the class functions are boilerplate and need simple string replacements with your module name. The rest of the functions below will require bigger changes.

3. GPO stands for "Group Policy Object" and allows for administrators to configure settings across a network of machines. It is required that your module is on this list of settings. You can right click the `powertoys_gpo` object to go to the definition and set up the `getConfiguredModuleEnabledValue` for your module.
```c++
virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
{
    return powertoys_gpo::getConfiguredModuleEnabledValue();
}
```

4. `init_settings()` initializes the settings for the interface. Will either pull from existing settings.json or use defaults.
```c++
void ModuleInterface::init_settings()
```

5. `get_config` retrieves the settings from the settings.json file.
```c++
virtual bool get_config(wchar_t* buffer, int* buffer_size) override
```

6. `set_config` sets the new settings to the settings.json file.
```c++
virtual void set_config(const wchar_t* config) override
```

7. `call_custom_action` allows custom actions to be called based on signals from the settings app.
```c++
void call_custom_action(const wchar_t* action) override
```

8. Lifecycle events control the whether the module is enabled or not as well as the default status of the module.
```c++
virtual void enable() // starts the module
virtual void disable() // terminates the module and performs any cleanup
virtual bool is_enabled() // returns if the module is currently enabled
virtual bool is_enabled_by_default() const override // allows the module to dictate whether it should be enabled by default in the PowerToys app.
```

9. Hotkey functions control the status of the hotkey.
```c++
// takes the hotkey from settings into a format the interface can understand
void parse_hotkey(PowerToysSettings::PowerToyValues& settings) 

// returns the hotkeys from settings
virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override 

// performs logic when the hotkey event is fired
virtual bool on_hotkey(size_t hotkeyId) override 
```

### Notes
* Keep module logic isolated under `/modules/<YourModule>`
* Use shared utilities from [`common`](https://github.com/microsoft/PowerToys/tree/main/src/common) instead of cross-module dependencies
* init/set/get config use preset functions to access the settings. Check out the [`settings_objects.h`](https://github.com/microsoft/PowerToys/blob/main/src/common/SettingsAPI/settings_helpers.h) in `src\common\SettingsAPI`

---
## 3. Bootstrapping Your Module
1. Use the [template](https://github.com/microsoft/PowerToys/tree/main/tools/project_template) to generate the module interface starter code.
2. Update all projects and namespaces with your module name.
3. Update GUIDs in `.vcxproj` and solution files.
4. Update the functions mentioned in the above section with your custom logic.
5. In order for your module to be detected by the runner you are required to add  references to various lists. In order to register your module, add the corresponding module reference to the lists that can be found in the following files. (Hint: search other modules names to find the lists quicker)
    * `src/runner/modules.h`
    * `src/runner/modules.cpp`
    * `src/runner/resource.h`
    * `src/runner/settings_window.h`
    * `src/runner/settings_window.cpp`
    * `src/runner/main.cpp`
    * `src/common/logger.h` (for logging)
6. ModuleInterface should build your `ModuleInterface.dll`. This will allow the runner to interact with your service.

> **Gotcha:** Mismatched module IDs are one of the most common causes of load failures. Keep your ID consistent across manifest, registry, and service.

---
## 4. Write your service
This is going to look different for every PowerToy. It may be easier to develop the application independently, and then link in the PowerToys settings logic later. But you have to write the service first before connecting it to the runner.

### Notes
* This is a separate project from the Module Interface.
* You can develop this project using C# or C++.
* Set the service icon using the `.rc` file.
* Set the service name in the `.vcxproj` by setting the `<TargetName>`
```
<PropertyGroup>
  <OutDir>..\..\..\..\$(Platform)\$(Configuration)\$(MSBuildProjectName)\</OutDir>
  <TargetName>PowerToys.LightSwitchService</TargetName>
</PropertyGroup>
```
* To view the code of the `.vcxproj` right click the item and press `Unload project`
* Use the following functions to interact with settings from your service
```
ModuleSettings::instance().InitFileWatcher();
ModuleSettings::instance().LoadSettings();
auto& settings = ModuleSettings::instance().settings();
```
These come from the `ModuleSettings.h` file that lives with the Service. You can copy this from another module (e.g., Light Switch) and adjust to fit your needs.

If your module has a user interface:
* Use the **WinUI Blank App** template when setting up your project
* Use [Windows design best practices](https://learn.microsoft.com/windows/apps/design/basics/)
* Use the [WinUI 3 Gallery](https://apps.microsoft.com/detail/9p3jfpwwdzrc) for help with your UI code, and additional guidance.

## 5. Settings Integration

PowerToys settings are stored per-module as JSON under:

```
%LOCALAPPDATA%\Microsoft\PowerToys\<module>\settings.json
```

### Implementation Steps

* In `src\settings-ui\Settings.UI.Library\` create `<module>Properties.cs` and `<module>Settings.cs`
* `<module>Properties.cs` is where you will define your defaults. Every setting needs to be represented here. This should match what was set in the Module Interface.
* `<module>Settings.cs`is where your settings.json will be built from. The structure should match the following
```cs
public ModuleSettings()
{
    Name = ModuleName;
    Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
    Properties = new ModuleProperties(); // settings properties you set above.
}
```
* In `src\settings-ui\Settings.UI\ViewModels` create `<module>ViewModel.cs` this is where the interaction happens between your settings page in the PowerToys app and the settings file that is stored on the device. Changes here will trigger the settings watcher via a `NotifyPropertyChanged` event.
* Create a `SettingsPage.xaml` at `src\settings-ui\Settings.UI\SettingsXAML\Views` this will be how the user interacts with the settings of your module.
* Be sure to use resource strings for user facing strings so they can later be localized. (x:Uid connects to Resources.resw)
```xaml
// LightSwitch.xaml
<ComboBoxItem
    x:Uid="LightSwitch_ModeOff"
    AutomationProperties.AutomationId="OffCBItem_LightSwitch"
    Tag="Off" />

// Resources.resw
<data name="LightSwitch_ModeOff.Content" xml:space="preserve">
  <value>Off</value>
</data>
```
> Note: in the above example we use `.Content` to target the content of the Combobox. This can change per UI element (e.g., `.Text`, `.Header`, etc.)

> **Reminder:** Manual edits in external editors (VS Code, Notepad) do **not** trigger the settings watcher. Only changes written through PowerToys trigger reloads.

---

### Gotchas:
* Only use the WinUI 3 framework, _not_ UWP.
* Use [`DispatcherQueue`](https://learn.microsoft.com/windows/apps/develop/dispatcherqueue) when updating UI from non-UI threads.

---
## 6. Building and Debugging
### Debugging steps
1. If this is your first time debugging PowerToys be sure to follow [these steps first](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/development/debugging.md#pre-debugging-setup).
2. Set "runner" as the start up project and ensure your build configuration is set to match your system (ARM64/x64)
3. Press `F5` or the "Local Windows Debugger" button to begin debugging. This should start the PowerToys runner.
4. To set breakpoints in your service, press `Ctrl + Alt + P` and search for your service to attach to the runner.
5. Use logs to document changes. The logs live at `%LOCALAPPDATA%\Microsoft\PowerToys\RunnerLogs` and `%LOCALAPPDATA%\Microsoft\PowerToys\Module\Service\<version>` for the specific module.

> **Gotcha:** PowerToys caches `.nuget` artifacts aggressively. Use `git clean -xfd` when builds behave unexpectedly.
---
## 7. Installer and Packaging (WiX)

### Add Your Module to Installer

1. Install [`WixToolset.Heat`](https://www.nuget.org/packages/WixToolset.Heat/) for Wix5 via nuget
2. Inside `installer\PowerToysInstallerVNext` add a new file for your module: `Module.wxs`
3. Inside of this file you will need copy the format from another module (ie: Light Switch) and replace the strings and GUID values.
4. The key part will be `<!--ModuleNameFiles_Component_Def-->` which is a placeholder for code that will be generated by `generateFileComponents.ps1`.
5. Inside `Product.wxs` add a line item in the `<Feature Id="CoreFeature" ... >` section. It will look like a list of ` <ComponentGroupRef Id="ModuleComponentGroup" />` items.
6. Inside `generateFileComponents.ps1` you will need to add an entry to the bottom for your new module. It will follow the following format. `-fileListName <Module>Files` will match the string you set in `Module.wxs`, `<ModuleServiceName>` will match the name of your exe.
```bash
# Module Name
Generate-FileList -fileDepsJson "" -fileListName <Module>Files -wxsFilePath $PSScriptRoot\<Module>.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\<ModuleServiceName>"
Generate-FileComponents -fileListName "<Module>Files" -wxsFilePath $PSScriptRoot\<Module>.wxs -regroot $registryroot
```
---
## 8. Testing and Validation

### UI Tests
* Place under `/modules/<YourModule>/Tests`
* Create a new [WinUI Unit Test App](https://learn.microsoft.com/windows/apps/winui/winui3/testing/create-winui-unit-test-project)
* Write unit tests following the format from previous modules (ie: Light Switch). This can be to test your standalone UI (if you're a module like Color Picker) or to verify that the Settings UI in the PowerToys app is controlling your service.

### Manual Validation
* Enable/disable in PowerToys Settings
* Check initialization in logs
* Confirm icons, tooltips, and OOBE page appear correctly

### Pro Tips:
1. Validate wake/sleep and elevation states. Background modules often fail silently after resume if event handles aren’t recreated.
2. Use Windows Sandbox to simulate clean install environments
3. To simulate a "new user" you can delete the PowerToys folder from `%LOCALAPPDATA%\Microsoft`

### Shortcut Conflict Detection
If your module has a shortcut, ensure that it is properly registered following [the steps listed in the documentation](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/settings/settings-implementation.md#shortcut-conflict-detection) for conflict detection.

---
## 9. The Final Touches

### Out-of-Box Experience (OOBE) page
The OOBE page is a custom settings page that gives the user at a glance information about each module. This Window opens first before the Settings application for new users and after updates. Create `OOBE<ModuleName>.xaml` at ` src\settings-ui\Settings.UI\SettingsXAML\OOBE\Views`. You will also need to add your module name to the enum at `src\settings-ui\Settings.UI\OOBE\Enums\PowerToysModules.cs`.

### Module Assets
Now that your PowerToy is _done_ you can start to think about the assets that will represent your module.
- Module Icon: This will be displayed in a number of places: OOBE page, in the README, on the home screen of PowerToys, on your individual module settings page, etc.  
- Module Image: This is the image you see at the top of each individual settings page.
- OOBE Image: This is the header you see on the OOBE page for each module

> Note: This step is something that the Design team will handle internally to ensure consistency throughout the application. If you have ideas or recommendations on what the icon or screenshots should be for your module feel free to leave it in the "Additional Comments" section of the PR and the team will take it into consideration.

### Documentation
There are two types of documentation that will be required when submitting a new PowerToy:
1. Developer documentation: This will live in the [PowerToys repo](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/modules) at `/doc/devdocs/modules/` and should tell a developer how to work on your app. It should outline the module architecture, key files, testing, and tips on debugging if necessary.
2. Learn documentation: When your new Module is set to be merged into the PowerToys repository an internal team member will create Microsoft Learn documentation so that users will understand how to use your module. There is not much work on your end as the developer for this step, but keep an eye on your PR in case we need more information about your PowerToy for this step.

---
Thank you again for contributing! If you need help, feel free to [open an issue](https://github.com/microsoft/PowerToys/issues/new/choose) and use the `Needs-Team-Response` label so we know you need attention.
