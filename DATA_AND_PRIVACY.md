# PowerToys Data & Privacy
## Overview
PowerToys diagnostic data is completely optional for users and is off by default in v0.86 and beyond. Our team believes in transparency and trust. As PowerToys is open source, all of our diagnostic data events are in the codebase.

Additionally, this document aims to list each diagnostic data event individually and describe their purpose clearly.

For more information, please read the [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement). 

## What does PowerToys collect?

1. **Usage**: Understanding usage and frequency rates for utilities and settings helps us make decisions on where to focus our time and energy. This data also helps us better understand what and how to move well-loved features directly into Windows!
2. **Stability**: Monitoring bugs and system crashes, as well as analyzing GitHub issue reports, assists us in prioritizing the most urgent issues.
3. **Performance**: Assessing the performance of PowerToys features to load and execute gives us an understanding of what surfaces are causing slowdowns. This supports our commitment to providing you with tools that are both speedy and effective.

### Success Story: Fixing FancyZones Bugs with Your Help
FancyZones had numerous bug reports related to virtual desktop interactions. Initially, these were considered lower priority, since the assumption was that virtual desktops were not widely used, so we chose to focus on more urgent issues. However, the volume of bug reports suggested otherwise, prompting us to add additional diagnostics to see virtual desktop usage with FancyZones. We discovered that virtual desktop usage was much higher among FancyZones users. This new understanding led us to prioritize this class of bugs and get them fixed.
 
## Transparency and Public Sharing
As much as possible, we aim to share the results of diagnostic data publicly.

We hope this document provides clarity on why and how we collect diagnostic data to improve PowerToys for our users. If you have any questions or concerns, please feel free to reach out to us.

Thank you for using PowerToys!

## List of Diagnostic Data Events
_**Note:** We're in the process of updating this section with more events and their descriptions. We aim to keep this list current by adding any new diagnostic data events as they become available._

_If you want to find diagnostic data events in the source code, these two links will be good starting points based on the source code's langauge._
- [C# events](https://github.com/search?q=repo%3Amicrosoft/PowerToys%20EventBase&type=code)
- [C++ events](https://github.com/search?q=repo%3Amicrosoft%2FPowerToys+ProjectTelemetryPrivacyDataTag&type=code)

### General
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.DebugEvent</td>
    <td>Logs debugging information for diagnostics and troubleshooting.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.GeneralSettingsChanged</td>
    <td>Logs changes made to general settings within PowerToys.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Runner_Launch</td>
    <td>Indicates when the PowerToys Runner is launched.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.SettingsBootEvent</td>
    <td>Triggered when PowerToys settings are initialized at startup.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.SettingsEnabledEvent</td>
    <td>Indicates that the PowerToys settings have been enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ScoobeStartedEvent</td>
    <td>Triggered when SCOOBE (Secondary Out-of-box experience) starts.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.TrayFlyoutActivatedEvent</td>
    <td>Indicates when the tray flyout menu is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.TrayFlyoutModuleRunEvent</td>
    <td>Logs when a utility from the tray flyout menu is run.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Uninstall_Success</td>
    <td>Logs when PowerToys is successfully uninstalled (who would do such a thing!).</td>
  </tr>
</table>

### OOBE (Out-of-box experience)
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.OobeSectionEvent</td>
    <td>Occurs when OOBE is shown to the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.OobeSettingsEvent</td>
    <td>Triggers when a Settings page is opened from an OOBE page.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.OobeStartedEvent</td>
    <td>Indicates when the out-of-box experience has been initiated.</td>
  </tr>
</table>

### Advanced Paste
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPaste_EnableAdvancedPaste</td>
    <td>Triggered when Advanced Paste is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPaste_Error</td>
    <td>Occurs when an error is encountered during the Advanced Paste process.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPaste_InvokeAdvancedPaste</td>
    <td>Activated when Advanced Paste is called by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPaste_Settings</td>
    <td>Triggered when settings for Advanced Paste are accessed or modified.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteClipboardItemClicked</td>
    <td>Occurs when a clipboard item is selected from the Advanced Paste menu.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteClipboardItemDeletedEvent</td>
    <td>Triggered when an item is removed from the Advanced Paste clipboard history.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteCustomFormatOutputThumbUpDownEvent</td>
    <td>Triggered when a user gives feedback on a custom format output (thumb up/down).</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteFormatClickedEvent</td>
    <td>Occurs when a specific paste format is clicked in the Advanced Paste menu.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteGenerateCustomErrorEvent</td>
    <td>Triggered when an error occurs while generating a custom paste format.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteGenerateCustomFormatEvent</td>
    <td>Occurs when a custom paste format is successfully generated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteInAppKeyboardShortcutEvent</td>
    <td>Triggered when a keyboard shortcut is used within the Advanced Paste interface.</td>
  </tr>  
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteSemanticKernelFormatEvent</td>
    <td>Triggered when Advanced Paste leverages the Semantic Kernel.</td>
  </tr> 
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteSemanticKernelErrorEvent</td>
    <td>Occurs when the Semantic Kernel workflow encounters an error.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteEndpointUsageEvent</td>
    <td>Logs the AI provider, model, and processing duration for each endpoint call.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AdvancedPasteCustomActionErrorEvent</td>
    <td>Records provider, model, and status details when a custom action fails.</td>
  </tr>
</table>

### Always on Top
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AlwaysOnTop_EnableAlwaysOnTop</td>
    <td>Triggered when Always on Top is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AlwaysOnTop_PinWindow</td>
    <td>Occurs when a window is pinned to stay on top of other windows.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AlwaysOnTop_UnpinWindow</td>
    <td>Triggered when a pinned window is unpinned, allowing it to be behind other windows.</td>
  </tr>
</table>

### Awake
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Awake_EnableAwake</td>
    <td>Triggered when Awake is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AwakeExpirableKeepAwakeEvent</td>
    <td>Occurs when the system is kept awake for a temporary, expirable duration.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AwakeIndefinitelyKeepAwakeEvent</td>
    <td>Triggered when the system is set to stay awake indefinitely.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AwakeNoKeepAwakeEvent</td>
    <td>Occurs when Awake is turned off, allowing the computer to enter sleep mode.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.AwakeTimedKeepAwakeEvent</td>
    <td>Triggered when the system is kept awake for a specified time duration.</td>
  </tr>  
</table>

### Color Picker
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ColorPicker_EnableColorPicker</td>
    <td>Triggered when Color Picker is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ColorPicker_Session</td>
    <td>Occurs during a Color Picker usage session.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ColorPicker_Settings</td>
    <td>Triggered when the settings for the Color Picker are accessed or modified.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ColorPickerCancelledEvent</td>
    <td>Occurs when a color picking action is cancelled by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ColorPickerShowEvent</td>
    <td>Triggered when the Color Picker UI is displayed on the screen.</td>
  </tr>  
</table>

### Command Not Found
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CmdNotFoundInstallEvent</td>
    <td>Triggered when a Command Not Found is installed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CmdNotFoundInstanceCreatedEvent</td>
    <td>Occurs when an instance of a Command Not Found is created.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CmdNotFoundUninstallEvent</td>
    <td>Triggered when Command Not Found is uninstalled after being previously installed.</td>
  </tr>  
</table>

### Crop And Lock
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CropAndLock_ActivateReparent</td>
    <td>Triggered when the cropping interface is activated for reparenting the cropped content.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CropAndLock_ActivateThumbnail</td>
    <td>Occurs when the thumbnail view for cropped content is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CropAndLock_EnableCropAndLock</td>
    <td>Triggered when Crop and Lock is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.CropAndLock_Settings</td>
    <td>Occurs when settings related to Crop and Lock are modified.</td>
  </tr>  
</table>

### Environment Variables
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.EnvironmentVariables_Activate</td>
    <td>Triggered when Environment Variables is launched.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.EnvironmentVariables_EnableEnvironmentVariables</td>
    <td>Occurs when Environment Variables is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.EnvironmentVariablesOpenedEvent</td>
    <td>Triggered when the Environment Variables interface is opened.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.EnvironmentVariablesProfileEnabledEvent</td>
    <td>Occurs when an environment variable profile is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.EnvironmentVariablesVariableChangedEvent</td>
    <td>Triggered when an environment variable is added, modified, or deleted.</td>
  </tr>  
</table>

### FancyZones
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_CycleActiveZoneSet</td>
    <td>Triggered when the active zone set is cycled through.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_EditorLaunch</td>
    <td>Occurs when the FancyZones editor is launched.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_EnableFancyZones</td>
    <td>Occurs when FancyZones is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_KeyboardSnapWindowToZone</td>
    <td>Triggered when a window is snapped to a zone using the keyboard.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_MoveOrResizeEnded</td>
    <td>Occurs when a window move or resize action has completed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_MoveOrResizeStarted</td>
    <td>Triggered when a window move or resize action is initiated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_MoveSizeEnd</td>
    <td>Occurs when the moving or resizing of a window has ended.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_OnKeyDown</td>
    <td>Triggered when a key is pressed down while interacting with zones.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_QuickLayoutSwitch</td>
    <td>Occurs when a quick switch between zone layouts is performed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_Settings</td>
    <td>Triggered when FancyZones settings are accessed or modified.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_SettingsChanged</td>
    <td>Occurs when there is a change in the FancyZones settings.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_SnapNewWindowIntoZone</td>
    <td>Triggered when a new window is snapped into a zone.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_VirtualDesktopChanged</td>
    <td>Occurs when the virtual desktop changes, affecting zone layout.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_ZoneSettingsChanged</td>
    <td>Triggered when the settings for specific zones are altered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FancyZones_ZoneWindowKeyUp</td>
    <td>Occurs when a key is released while interacting with zones.</td>
  </tr>
</table>

### FileExplorerAddOns
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.GcodeFileHandlerLoaded</td>
    <td>Triggered when a G-code file handler is loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.GcodeFilePreviewed</td>
    <td>Occurs when a G-code file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.GcodeFilePreviewError</td>
    <td>Triggered when there is an error previewing a G-code file.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.BgcodeFileHandlerLoaded</td>
    <td>Triggered when a Binary G-code file handler is loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.BgcodeFilePreviewed</td>
    <td>Occurs when a Binary G-code file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.BgcodeFilePreviewError</td>
    <td>Triggered when there is an error previewing a Binary G-code file.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MarkdownFileHandlerLoaded</td>
    <td>Occurs when a Markdown file handler is loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MarkdownFilePreviewed</td>
    <td>Triggered when a Markdown file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PdfFileHandlerLoaded</td>
    <td>Occurs when a PDF file handler is loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PdfFilePreviewed</td>
    <td>Triggered when a PDF file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerPreview_Enabled</td>
    <td>Occurs when preview is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerPreview_TweakUISettings_Destroyed</td>
    <td>Triggered when the Tweak UI settings for Power Preview are destroyed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerPreview_TweakUISettings_FailedUpdatingSettings</td>
    <td>Occurs when updating Tweak UI settings fails.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerPreview_TweakUISettings_InitSet__ErrorLoadingFile</td>
    <td>Triggered when there is an error loading a file during Tweak UI settings initialization.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerPreview_TweakUISettings_SuccessfullyUpdatedSettings</td>
    <td>Occurs when the Tweak UI settings for Power Preview are successfully updated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.QoiFilePreviewed</td>
    <td>Triggered when a QOI file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.SvgFileHandlerLoaded</td>
    <td>Occurs when an SVG file handler is loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.SvgFilePreviewed</td>
    <td>Triggered when an SVG file is previewed in File Explorer.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.SvgFilePreviewError</td>
    <td>Occurs when there is an error previewing an SVG file.</td>
  </tr>
</table>

### File Locksmith
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FileLocksmith_EnableFileLocksmith</td>
    <td>Triggered when File Locksmith is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FileLocksmith_Invoked</td>
    <td>Occurs when File Locksmith is invoked.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FileLocksmith_InvokedRet</td>
    <td>Triggered when File Locksmith invocation returns a result.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FileLocksmith_QueryContextMenuError</td>
    <td>Occurs when there is an error querying the context menu for File Locksmith.</td>
  </tr>
</table>

### Find My Mouse
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FindMyMouse_EnableFindMyMouse</td>
    <td>Triggered when Find My Mouse is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.FindMyMouse_MousePointerFocused</td>
    <td>Occurs when the mouse pointer is focused using Find My Mouse.</td>
  </tr>
</table>

### Hosts File Editor
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.HostsFileEditor_Activate</td>
    <td>Triggered when Hosts File Editor is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.HostsFileEditor_EnableHostsFileEditor</td>
    <td>Occurs when Hosts File Editor is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.HostsFileEditorOpenedEvent</td>
    <td>Fires when Hosts File Editor is opened.</td>
  </tr>
</table>

### Image Resizer
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ImageResizer_EnableImageResizer</td>
    <td>Triggered when Image Resizer is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ImageResizer_Invoked</td>
    <td>Occurs when Image Resizer is invoked by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ImageResizer_InvokedRet</td>
    <td>Fires when the Image Resizer operation is completed and returns a result.</td>
  </tr>
</table>

### Keyboard Manager
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutRemapConfigurationLoaded</td>
    <td>Indicates that the application-specific shortcut remap configuration has been successfully loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutRemapCount</td>
    <td>Logs the number of application-specific shortcut remaps configured by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutToShortcutRemapInvoked</td>
    <td>Logs each instance when an application-specific shortcut-to-shortcut remap is used.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyAppSpecificShortcutToKeyRemapInvoked</td>
    <td>Logs the daily count of application-specific shortcut-to-key remaps executed by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyAppSpecificShortcutToShortcutRemapInvoked</td>
    <td>Logs the daily count of application-specific shortcut-to-shortcut remaps executed by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyKeyToKeyRemapInvoked</td>
    <td>Logs the daily count of key-to-key remaps used by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyKeyToShortcutRemapInvoked</td>
    <td>Logs the daily count of key-to-shortcut remaps used by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyShortcutToKeyRemapInvoked</td>
    <td>Logs the daily count of shortcut-to-key remaps used by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_DailyShortcutToShortcutRemapInvoked</td>
    <td>Logs the daily count of shortcut-to-shortcut remaps used by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_EnableKeyboardManager</td>
    <td>Indicates that the Keyboard Manager has been enabled in PowerToys settings.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_KeyRemapConfigurationLoaded</td>
    <td>Indicates that the key remap configuration has been successfully loaded.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_KeyRemapCount</td>
    <td>Logs the number of individual key remaps configured by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_KeyToKeyRemapInvoked</td>
    <td>Logs each instance of a key-to-key remap being used.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_KeyToShortcutRemapInvoked</td>
    <td>Logs each instance of a key-to-shortcut remap being used.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_OSLevelShortcutRemapCount</td>
    <td>Logs the total number of OS-level shortcut remaps configured by the user.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_OSLevelShortcutToKeyRemapInvoked</td>
    <td>Logs each instance of an OS-level shortcut-to-key remap being used.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_OSLevelShortcutToShortcutRemapInvoked</td>
    <td>Logs each instance of an OS-level shortcut-to-shortcut remap being used.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.KeyboardManager_ShortcutRemapConfigurationLoaded</td>
    <td>Indicates that the shortcut remap configuration has been successfully loaded.</td>
  </tr>
</table>

### Mouse Highlighter
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseHighlighter_EnableMouseHighlighter</td>
    <td>Triggered when Mouse Highlighter is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseHighlighter_StartHighlightingSession</td>
    <td>Occurs when a new highlighting session is started.</td>
  </tr>
</table>

### Mouse Jump
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseJump_EnableJumpTool</td>
    <td>Triggered when Mouse Jump is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseJump_InvokeJumpTool</td>
    <td>Occurs when Mouse Jump is invoked.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseJumpShowEvent</td>
    <td>Triggered when the Mouse Jump display is shown.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseJumpTeleportCursorEvent</td>
    <td>Occurs when the cursor is teleported to a new location.</td>
  </tr>
</table>

### Mouse Pointer Crosshairs
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MousePointerCrosshairs_EnableMousePointerCrosshairs</td>
    <td>Triggered when Mouse Pointer Crosshairs is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MousePointerCrosshairs_StartDrawingCrosshairs</td>
    <td>Occurs when the crosshairs are drawn around the mouse pointer.</td>
  </tr>
</table>

### Mouse Without Borders
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBorders_Activate</td>
    <td>Triggered when Mouse Without Borders is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBorders_AddFirewallRule</td>
    <td>Occurs when a firewall rule is added for Mouse Without Borders.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBorders_EnableMouseWithoutBorders</td>
    <td>Triggered when Mouse Without Borders is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBorders_ToggleServiceRegistration</td>
    <td>Occurs when the service registration for Mouse Without Borders is toggled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersClipboardFileTransferEvent</td>
    <td>Triggered during a clipboard file transfer between computers.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersDragAndDropEvent</td>
    <td>Occurs during a drag-and-drop operation between computers.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersMultipleModeEvent</td>
    <td>Triggered when multiple modes are enabled in Mouse Without Borders.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersOldUIOpenedEvent</td>
    <td>Occurs when the old user interface for Mouse Without Borders is opened.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersOldUIQuitEvent</td>
    <td>Triggered when the old user interface for Mouse Without Borders is closed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersOldUIReconfigureEvent</td>
    <td>Occurs when the old user interface for Mouse Without Borders is reconfigured.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MouseWithoutBordersStartedEvent</td>
    <td>Triggered when Mouse Without Borders is started.</td>
  </tr>
</table>

### New+
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.NewPlus_EventCopyTemplate</td>
    <td>Triggered when an item from New+ is created (copied to the current directory).</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.NewPlus_EventCopyTemplateResult</td>
    <td>Logs the success of item creation (copying).</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.NewPlus_EventShowTemplateItems</td>
    <td>Triggered when the New+ context menu flyout is displayed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.NewPlus_EventToggleOnOff</td>
    <td>Triggered when New+ is enabled or disabled.</td>
  </tr>
</table>

### Peek
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_Closed</td>
    <td>Triggered when Peek is closed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_EnablePeek</td>
    <td>Occurs when Peek is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_Error</td>
    <td>Triggered when an error occurs for Peek.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_InvokePeek</td>
    <td>Occurs when Peek is invoked.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_Opened</td>
    <td>Triggered when a Peek window is opened.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_OpenWith</td>
    <td>Occurs when an item is opened with Peek.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Peek_Settings</td>
    <td>Triggered when the settings for Peek are modified.</td>
  </tr>
</table>

### PowerRename
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_EnablePowerRename</td>
    <td>Triggered when PowerRename is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_Invoked</td>
    <td>Occurs when PowerRename is invoked.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_InvokedRet</td>
    <td>Triggered when the invocation of PowerRename returns a result.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_RenameOperation</td>
    <td>Triggered during the rename operation within PowerRename.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_SettingsChanged</td>
    <td>Occurs when the settings for PowerRename are changed.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerRename_UIShownRet</td>
    <td>Triggered when the PowerRename user interface is shown.</td>
  </tr>
</table>

### PowerToys Run
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherBootEvent</td>
    <td>Triggered when PowerToys Run is initialized on boot.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherColdStateHotkeyEvent</td>
    <td>Occurs when the hotkey is pressed in the cold state (not yet initialized).</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherFirstDeleteEvent</td>
    <td>Triggered when the first deletion action is performed in PowerToys Run.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherHideEvent</td>
    <td>Occurs when PowerToys Run is hidden.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherQueryEvent</td>
    <td>Triggered when a query is made in PowerToys Run.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherResultActionEvent</td>
    <td>Occurs when an action is taken on a result in PowerToys Run.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherShowEvent</td>
    <td>Triggered when PowerToys Run is shown.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.LauncherWarmStateHotkeyEvent</td>
    <td>Occurs when the hotkey is pressed in the warm state (initialized).</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.RunPluginsSettingsEvent</td>
    <td>Triggered when the settings for PowerToys Run plugins are accessed or modified.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.WindowWalker_EnableWindowWalker</td>
    <td>Triggered when the Window Walker plugin is enabled.</td>
  </tr>
</table>

### Quick Accent
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerAccent_EnablePowerAccent</td>
    <td>Triggered when Quick Accent is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerAccentShowAccentMenuEvent</td>
    <td>Occurs when the accent menu is displayed.</td>
  </tr>
</table>

### Registry Preview
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.RegistryPreview_Activate</td>
    <td>Triggered when Registry Preview is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.RegistryPreview_EnableRegistryPreview</td>
    <td>Occurs when Registry Preview is enabled.</td>
  </tr>
</table>

### Screen Ruler
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MeasureTool_BoundsToolActivated</td>
    <td>Triggered when Screen Ruler's Bounds tool is activated.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MeasureTool_EnableMeasureTool</td>
    <td>Occurs when Screen Ruler is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.MeasureTool_MeasureToolActivated</td>
    <td>Triggered when Screen Ruler's Measure tool is activated.</td>
  </tr>
</table>

### Shortcut Guide
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ShortcutGuide_EnableGuide</td>
    <td>Triggered when Shortcut Guide is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ShortcutGuide_HideGuide</td>
    <td>Occurs when Shortcut Guide is hidden from view.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ShortcutGuide_Settings</td>
    <td>Indicates a change in the settings related to the Shortcut Guide.</td>
  </tr>
</table>

### Text Extractor
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerOCR_EnablePowerOCR</td>
    <td>Triggered when the Text Extractor (OCR) feature is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerOCRCancelledEvent</td>
    <td>Occurs when the text extraction process is cancelled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerOCRCaptureEvent</td>
    <td>Occurs when the user has created a capture for text extraction.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.PowerOCRInvokedEvent</td>
    <td>Triggered when Text Extractor is invoked.</td>
  </tr>
</table>

### Workspaces
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Projects_CLIUsage</td>
    <td>Logs usage of command-line arguments for launching apps.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_CreateEvent</td>
    <td>Triggered when a new workspace is created.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_DeleteEvent</td>
    <td>Triggered when a workspace is deleted.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_EditEvent</td>
    <td>Triggered when a workspace is edited or modified.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_Enable</td>
    <td>Indicates that Workspaces is enabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_LaunchEvent</td>
    <td>Triggered when a workspace is launched.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.Workspaces_Settings</td>
    <td>Logs changes to workspaces settings.</td>
  </tr>
</table>

### ZoomIt
<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_EnableZoomIt</td>
    <td>Triggered when ZoomIt is enabled/disabled.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_Started</td>
    <td>Triggered when the ZoomIt process starts.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateBreak</td>
    <td>Triggered when the Break mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateDraw</td>
    <td>Triggered when the Draw mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateZoom</td>
    <td>Triggered when the Zoom mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateLiveZoom</td>
    <td>Triggered when the Live Zoom mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateDemoType</td>
    <td>Triggered when the DemoType mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateRecord</td>
    <td>Triggered when the Record mode is entered.</td>
  </tr>
  <tr>
    <td>Microsoft.PowerToys.ZoomIt_ActivateSnip</td>
    <td>Triggered when the Snip mode is entered.</td>
  </tr>
</table>

<!-- back up of table

<table style="width:100%">
  <tr>
    <th>Event Name</th>
    <th>Description</th>
  </tr>
  <tr>
    <td>x</td>
    <td>x</td>
  </tr>
  <tr>
    <td>x</td>
    <td>x</td>
  </tr>
  <tr>
    <td>x</td>
    <td>x</td>
  </tr>
</table>
-->
