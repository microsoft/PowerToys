# PowerToys Data & Privacy

<style>
table { width: 100%; }
</style>

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

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.DebugEvent | Logs debugging information for diagnostics and troubleshooting. |
| Microsoft.PowerToys.GeneralSettingsChanged | Logs changes made to general settings within PowerToys. |
| Microsoft.PowerToys.Runner_Launch | Indicates when the PowerToys Runner is launched. |
| Microsoft.PowerToys.SettingsBootEvent | Triggered when PowerToys settings are initialized at startup. |
| Microsoft.PowerToys.SettingsEnabledEvent | Indicates that the PowerToys settings have been enabled. |
| Microsoft.PowerToys.ScoobeStartedEvent | Triggered when SCOOBE (Secondary Out-of-box experience) starts. |
| Microsoft.PowerToys.TrayFlyoutActivatedEvent | Indicates when the tray flyout menu is activated. |
| Microsoft.PowerToys.TrayFlyoutModuleRunEvent | Logs when a utility from the tray flyout menu is run. |
| Microsoft.PowerToys.Uninstall_Success | Logs when PowerToys is successfully uninstalled (who would do such a thing!). |

### OOBE (Out-of-box experience)

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.OobeSectionEvent | Occurs when OOBE is shown to the user. |
| Microsoft.PowerToys.OobeSettingsEvent | Triggers when a Settings page is opened from an OOBE page. |
| Microsoft.PowerToys.OobeStartedEvent | Indicates when the out-of-box experience has been initiated. |

### Advanced Paste

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.AdvancedPaste_EnableAdvancedPaste | Triggered when Advanced Paste is enabled. |
| Microsoft.PowerToys.AdvancedPaste_Error | Occurs when an error is encountered during the Advanced Paste process. |
| Microsoft.PowerToys.AdvancedPaste_InvokeAdvancedPaste | Activated when Advanced Paste is called by the user. |
| Microsoft.PowerToys.AdvancedPaste_Settings | Triggered when settings for Advanced Paste are accessed or modified. |
| Microsoft.PowerToys.AdvancedPasteClipboardItemClicked | Occurs when a clipboard item is selected from the Advanced Paste menu. |
| Microsoft.PowerToys.AdvancedPasteClipboardItemDeletedEvent | Triggered when an item is removed from the Advanced Paste clipboard history. |
| Microsoft.PowerToys.AdvancedPasteCustomFormatOutputThumbUpDownEvent | Triggered when a user gives feedback on a custom format output (thumb up/down). |
| Microsoft.PowerToys.AdvancedPasteFormatClickedEvent | Occurs when a specific paste format is clicked in the Advanced Paste menu. |
| Microsoft.PowerToys.AdvancedPasteGenerateCustomErrorEvent | Triggered when an error occurs while generating a custom paste format. |
| Microsoft.PowerToys.AdvancedPasteGenerateCustomFormatEvent | Occurs when a custom paste format is successfully generated. |
| Microsoft.PowerToys.AdvancedPasteInAppKeyboardShortcutEvent | Triggered when a keyboard shortcut is used within the Advanced Paste interface. |
| Microsoft.PowerToys.AdvancedPasteSemanticKernelFormatEvent | Triggered when Advanced Paste leverages the Semantic Kernel. |
| Microsoft.PowerToys.AdvancedPasteSemanticKernelErrorEvent | Occurs when the Semantic Kernel workflow encounters an error. |
| Microsoft.PowerToys.AdvancedPasteEndpointUsageEvent | Logs the AI provider, model, and processing duration for each endpoint call. |
| Microsoft.PowerToys.AdvancedPasteCustomActionErrorEvent | Records provider, model, and status details when a custom action fails. |

### Always on Top

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.AlwaysOnTop_EnableAlwaysOnTop | Triggered when Always on Top is enabled. |
| Microsoft.PowerToys.AlwaysOnTop_PinWindow | Occurs when a window is pinned to stay on top of other windows. |
| Microsoft.PowerToys.AlwaysOnTop_UnpinWindow | Triggered when a pinned window is unpinned, allowing it to be behind other windows. |

### Awake

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.Awake_EnableAwake | Triggered when Awake is enabled. |
| Microsoft.PowerToys.AwakeExpirableKeepAwakeEvent | Occurs when the system is kept awake for a temporary, expirable duration. |
| Microsoft.PowerToys.AwakeIndefinitelyKeepAwakeEvent | Triggered when the system is set to stay awake indefinitely. |
| Microsoft.PowerToys.AwakeNoKeepAwakeEvent | Occurs when Awake is turned off, allowing the computer to enter sleep mode. |
| Microsoft.PowerToys.AwakeTimedKeepAwakeEvent | Triggered when the system is kept awake for a specified time duration. |

### Color Picker

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.ColorPicker_EnableColorPicker | Triggered when Color Picker is enabled. |
| Microsoft.PowerToys.ColorPicker_Session | Occurs during a Color Picker usage session. |
| Microsoft.PowerToys.ColorPicker_Settings | Triggered when the settings for the Color Picker are accessed or modified. |
| Microsoft.PowerToys.ColorPickerCancelledEvent | Occurs when a color picking action is cancelled by the user. |
| Microsoft.PowerToys.ColorPickerShowEvent | Triggered when the Color Picker UI is displayed on the screen. |

### Command Not Found

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.CmdNotFound_EnableCmdNotFound | Triggered when Command Not Found is enabled or disabled. |
| Microsoft.PowerToys.CmdNotFoundInstallEvent | Triggered when a Command Not Found is installed. |
| Microsoft.PowerToys.CmdNotFoundInstanceCreatedEvent | Occurs when an instance of a Command Not Found is created. |
| Microsoft.PowerToys.CmdNotFoundUninstallEvent | Triggered when Command Not Found is uninstalled after being previously installed. |

### Command Palette

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.CmdPal_BeginInvoke | Triggered when the Command Palette is launched by the user. |
| Microsoft.PowerToys.CmdPal_ColdLaunch | Occurs when Command Palette starts for the first time (cold start). |
| Microsoft.PowerToys.CmdPal_OpenPage | Triggered when a page is opened within the Command Palette, tracking navigation depth. |
| Microsoft.PowerToys.CmdPal_OpenUri | Occurs when a URI is opened through the Command Palette, including whether it's a web URL. |
| Microsoft.PowerToys.CmdPal_ReactivateInstance | Triggered when an existing Command Palette instance is reactivated. |
| Microsoft.PowerToys.CmdPal_RunCommand | Logs when a command is executed through the Command Palette, including admin elevation status. |
| Microsoft.PowerToys.CmdPal_RunQuery | Triggered when a search query is performed, including result count and duration. |
| Microsoft.PowerToys.CmdPalDismissedOnEsc | Occurs when the Command Palette is dismissed by pressing the Escape key. |
| Microsoft.PowerToys.CmdPalDismissedOnLostFocus | Triggered when the Command Palette is dismissed due to losing focus. |
| Microsoft.PowerToys.CmdPalHotkeySummoned | Logs when the Command Palette is summoned via hotkey, distinguishing between global and context-specific hotkeys. |
| Microsoft.PowerToys.CmdPalInvokeResult | Records the result type of a Command Palette invocation. |
| Microsoft.PowerToys.CmdPalProcessStarted | Triggered when the Command Palette process is started. |
| Microsoft.PowerToys.CmdPal_ExtensionInvoked | Tracks extension usage including extension ID, command details, success status, and execution time. |
| Microsoft.PowerToys.CmdPal_SessionDuration | Logs session metrics from launch to dismissal including duration, commands executed, pages visited, search queries, navigation depth, and errors. |

### Crop And Lock

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.CropAndLock_ActivateReparent | Triggered when the cropping interface is activated for reparenting the cropped content. |
| Microsoft.PowerToys.CropAndLock_ActivateThumbnail | Occurs when the thumbnail view for cropped content is activated. |
| Microsoft.PowerToys.CropAndLock_EnableCropAndLock | Triggered when Crop and Lock is enabled. |
| Microsoft.PowerToys.CropAndLock_Settings | Occurs when settings related to Crop and Lock are modified. |

### Environment Variables

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.EnvironmentVariables_Activate | Triggered when Environment Variables is launched. |
| Microsoft.PowerToys.EnvironmentVariables_EnableEnvironmentVariables | Occurs when Environment Variables is enabled. |
| Microsoft.PowerToys.EnvironmentVariablesOpenedEvent | Triggered when the Environment Variables interface is opened. |
| Microsoft.PowerToys.EnvironmentVariablesProfileEnabledEvent | Occurs when an environment variable profile is enabled. |
| Microsoft.PowerToys.EnvironmentVariablesVariableChangedEvent | Triggered when an environment variable is added, modified, or deleted. |

### FancyZones

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.FancyZones_CycleActiveZoneSet | Triggered when the active zone set is cycled through. |
| Microsoft.PowerToys.FancyZones_EditorLaunch | Occurs when the FancyZones editor is launched. |
| Microsoft.PowerToys.FancyZones_EnableFancyZones | Occurs when FancyZones is enabled. |
| Microsoft.PowerToys.FancyZones_KeyboardSnapWindowToZone | Triggered when a window is snapped to a zone using the keyboard. |
| Microsoft.PowerToys.FancyZones_MoveOrResizeEnded | Occurs when a window move or resize action has completed. |
| Microsoft.PowerToys.FancyZones_MoveOrResizeStarted | Triggered when a window move or resize action is initiated. |
| Microsoft.PowerToys.FancyZones_MoveSizeEnd | Occurs when the moving or resizing of a window has ended. |
| Microsoft.PowerToys.FancyZones_OnKeyDown | Triggered when a key is pressed down while interacting with zones. |
| Microsoft.PowerToys.FancyZones_QuickLayoutSwitch | Occurs when a quick switch between zone layouts is performed. |
| Microsoft.PowerToys.FancyZones_Settings | Triggered when FancyZones settings are accessed or modified. |
| Microsoft.PowerToys.FancyZones_SettingsChanged | Occurs when there is a change in the FancyZones settings. |
| Microsoft.PowerToys.FancyZones_SnapNewWindowIntoZone | Triggered when a new window is snapped into a zone. |
| Microsoft.PowerToys.FancyZones_VirtualDesktopChanged | Occurs when the virtual desktop changes, affecting zone layout. |
| Microsoft.PowerToys.FancyZones_ZoneSettingsChanged | Triggered when the settings for specific zones are altered. |
| Microsoft.PowerToys.FancyZones_ZoneWindowKeyUp | Occurs when a key is released while interacting with zones. |
| Microsoft.PowerToys.FancyZones_CLICommand | Triggered when a FancyZones CLI command is executed, logging the command name and success status. |

### FileExplorerAddOns

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.GcodeFileHandlerLoaded | Triggered when a G-code file handler is loaded. |
| Microsoft.PowerToys.GcodeFilePreviewed | Occurs when a G-code file is previewed in File Explorer. |
| Microsoft.PowerToys.GcodeFilePreviewError | Triggered when there is an error previewing a G-code file. |
| Microsoft.PowerToys.BgcodeFileHandlerLoaded | Triggered when a Binary G-code file handler is loaded. |
| Microsoft.PowerToys.BgcodeFilePreviewed | Occurs when a Binary G-code file is previewed in File Explorer. |
| Microsoft.PowerToys.BgcodeFilePreviewError | Triggered when there is an error previewing a Binary G-code file. |
| Microsoft.PowerToys.MarkdownFileHandlerLoaded | Occurs when a Markdown file handler is loaded. |
| Microsoft.PowerToys.MarkdownFilePreviewed | Triggered when a Markdown file is previewed in File Explorer. |
| Microsoft.PowerToys.PdfFileHandlerLoaded | Occurs when a PDF file handler is loaded. |
| Microsoft.PowerToys.PdfFilePreviewed | Triggered when a PDF file is previewed in File Explorer. |
| Microsoft.PowerToys.PowerPreview_Enabled | Occurs when preview is enabled. |
| Microsoft.PowerToys.PowerPreview_TweakUISettings_Destroyed | Triggered when the Tweak UI settings for Power Preview are destroyed. |
| Microsoft.PowerToys.PowerPreview_TweakUISettings_FailedUpdatingSettings | Occurs when updating Tweak UI settings fails. |
| Microsoft.PowerToys.PowerPreview_TweakUISettings_InitSet__ErrorLoadingFile | Triggered when there is an error loading a file during Tweak UI settings initialization. |
| Microsoft.PowerToys.PowerPreview_TweakUISettings_SuccessfullyUpdatedSettings | Occurs when the Tweak UI settings for Power Preview are successfully updated. |
| Microsoft.PowerToys.QoiFilePreviewed | Triggered when a QOI file is previewed in File Explorer. |
| Microsoft.PowerToys.SvgFileHandlerLoaded | Occurs when an SVG file handler is loaded. |
| Microsoft.PowerToys.SvgFilePreviewed | Triggered when an SVG file is previewed in File Explorer. |
| Microsoft.PowerToys.SvgFilePreviewError | Occurs when there is an error previewing an SVG file. |

### File Locksmith

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.FileLocksmith_EnableFileLocksmith | Triggered when File Locksmith is enabled. |
| Microsoft.PowerToys.FileLocksmith_Invoked | Occurs when File Locksmith is invoked. |
| Microsoft.PowerToys.FileLocksmith_InvokedRet | Triggered when File Locksmith invocation returns a result. |
| Microsoft.PowerToys.FileLocksmith_QueryContextMenuError | Occurs when there is an error querying the context menu for File Locksmith. |

### Find My Mouse

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.FindMyMouse_EnableFindMyMouse | Triggered when Find My Mouse is enabled. |
| Microsoft.PowerToys.FindMyMouse_MousePointerFocused | Occurs when the mouse pointer is focused using Find My Mouse. |

### Hosts File Editor

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.HostsFileEditor_Activate | Triggered when Hosts File Editor is activated. |
| Microsoft.PowerToys.HostsFileEditor_EnableHostsFileEditor | Occurs when Hosts File Editor is enabled. |
| Microsoft.PowerToys.HostsFileEditorOpenedEvent | Fires when Hosts File Editor is opened. |

### Image Resizer

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.ImageResizer_EnableImageResizer | Triggered when Image Resizer is enabled. |
| Microsoft.PowerToys.ImageResizer_Invoked | Occurs when Image Resizer is invoked by the user. |
| Microsoft.PowerToys.ImageResizer_InvokedRet | Fires when the Image Resizer operation is completed and returns a result. |

### Keyboard Manager

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutRemapConfigurationLoaded | Indicates that the application-specific shortcut remap configuration has been successfully loaded. |
| Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutRemapCount | Logs the number of application-specific shortcut remaps configured by the user. |
| Microsoft.PowerToys.KeyboardManager_AppSpecificShortcutToShortcutRemapInvoked | Logs each instance when an application-specific shortcut-to-shortcut remap is used. |
| Microsoft.PowerToys.KeyboardManager_DailyAppSpecificShortcutToKeyRemapInvoked | Logs the daily count of application-specific shortcut-to-key remaps executed by the user. |
| Microsoft.PowerToys.KeyboardManager_DailyAppSpecificShortcutToShortcutRemapInvoked | Logs the daily count of application-specific shortcut-to-shortcut remaps executed by the user. |
| Microsoft.PowerToys.KeyboardManager_DailyKeyToKeyRemapInvoked | Logs the daily count of key-to-key remaps used by the user. |
| Microsoft.PowerToys.KeyboardManager_DailyKeyToShortcutRemapInvoked | Logs the daily count of key-to-shortcut remaps used by the user. |
| Microsoft.PowerToys.KeyboardManager_DailyShortcutToKeyRemapInvoked | Logs the daily count of shortcut-to-key remaps used by the user. |
| Microsoft.PowerToys.KeyboardManager_DailyShortcutToShortcutRemapInvoked | Logs the daily count of shortcut-to-shortcut remaps used by the user. |
| Microsoft.PowerToys.KeyboardManager_EnableKeyboardManager | Indicates that the Keyboard Manager has been enabled in PowerToys settings. |
| Microsoft.PowerToys.KeyboardManager_KeyRemapConfigurationLoaded | Indicates that the key remap configuration has been successfully loaded. |
| Microsoft.PowerToys.KeyboardManager_KeyRemapCount | Logs the number of individual key remaps configured by the user. |
| Microsoft.PowerToys.KeyboardManager_KeyToKeyRemapInvoked | Logs each instance of a key-to-key remap being used. |
| Microsoft.PowerToys.KeyboardManager_KeyToShortcutRemapInvoked | Logs each instance of a key-to-shortcut remap being used. |
| Microsoft.PowerToys.KeyboardManager_OSLevelShortcutRemapCount | Logs the total number of OS-level shortcut remaps configured by the user. |
| Microsoft.PowerToys.KeyboardManager_OSLevelShortcutToKeyRemapInvoked | Logs each instance of an OS-level shortcut-to-key remap being used. |
| Microsoft.PowerToys.KeyboardManager_OSLevelShortcutToShortcutRemapInvoked | Logs each instance of an OS-level shortcut-to-shortcut remap being used. |
| Microsoft.PowerToys.KeyboardManager_ShortcutRemapConfigurationLoaded | Indicates that the shortcut remap configuration has been successfully loaded. |

### Light Switch

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.LightSwitch_EnableLightSwitch | Triggered when Light Switch is enabled or disabled. |
| Microsoft.PowerToys.LightSwitch_ShortcutInvoked | Occurs when the shortcut for Light Switch is invoked. |
| Microsoft.PowerToys.LightSwitch_ScheduleModeToggled | Occurs when a new schedule mode is selected for Light Switch. |
| Microsoft.PowerToys.LightSwitch_ThemeTargetChanged | Occurs when the options for targeting the system or apps is updated. |

### Mouse Highlighter

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.MouseHighlighter_EnableMouseHighlighter | Triggered when Mouse Highlighter is enabled. |
| Microsoft.PowerToys.MouseHighlighter_StartHighlightingSession | Occurs when a new highlighting session is started. |

### Mouse Jump

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.MouseJump_EnableJumpTool | Triggered when Mouse Jump is enabled. |
| Microsoft.PowerToys.MouseJump_InvokeJumpTool | Occurs when Mouse Jump is invoked. |
| Microsoft.PowerToys.MouseJumpShowEvent | Triggered when the Mouse Jump display is shown. |
| Microsoft.PowerToys.MouseJumpTeleportCursorEvent | Occurs when the cursor is teleported to a new location. |

### Mouse Pointer Crosshairs

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.MousePointerCrosshairs_EnableMousePointerCrosshairs | Triggered when Mouse Pointer Crosshairs is enabled. |
| Microsoft.PowerToys.MousePointerCrosshairs_StartDrawingCrosshairs | Occurs when the crosshairs are drawn around the mouse pointer. |

### Mouse Without Borders

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.MouseWithoutBorders_Activate | Triggered when Mouse Without Borders is activated. |
| Microsoft.PowerToys.MouseWithoutBorders_AddFirewallRule | Occurs when a firewall rule is added for Mouse Without Borders. |
| Microsoft.PowerToys.MouseWithoutBorders_EnableMouseWithoutBorders | Triggered when Mouse Without Borders is enabled. |
| Microsoft.PowerToys.MouseWithoutBorders_ToggleServiceRegistration | Occurs when the service registration for Mouse Without Borders is toggled. |
| Microsoft.PowerToys.MouseWithoutBordersClipboardFileTransferEvent | Triggered during a clipboard file transfer between computers. |
| Microsoft.PowerToys.MouseWithoutBordersDragAndDropEvent | Occurs during a drag-and-drop operation between computers. |
| Microsoft.PowerToys.MouseWithoutBordersMultipleModeEvent | Triggered when multiple modes are enabled in Mouse Without Borders. |
| Microsoft.PowerToys.MouseWithoutBordersOldUIOpenedEvent | Occurs when the old user interface for Mouse Without Borders is opened. |
| Microsoft.PowerToys.MouseWithoutBordersOldUIQuitEvent | Triggered when the old user interface for Mouse Without Borders is closed. |
| Microsoft.PowerToys.MouseWithoutBordersOldUIReconfigureEvent | Occurs when the old user interface for Mouse Without Borders is reconfigured. |
| Microsoft.PowerToys.MouseWithoutBordersStartedEvent | Triggered when Mouse Without Borders is started. |

### New+

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.NewPlus_ChangedTemplateLocation | Triggered when the template folder location is changed. |
| Microsoft.PowerToys.NewPlus_EventCopyTemplate | Triggered when an item from New+ is created (copied to the current directory). |
| Microsoft.PowerToys.NewPlus_EventCopyTemplateResult | Logs the success of item creation (copying). |
| Microsoft.PowerToys.NewPlus_EventOpenTemplates | Triggered when the templates folder is opened. |
| Microsoft.PowerToys.NewPlus_EventShowTemplateItems | Triggered when the New+ context menu flyout is displayed. |
| Microsoft.PowerToys.NewPlus_EventToggleOnOff | Triggered when New+ is enabled or disabled. |

### Peek

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.Peek_Closed | Triggered when Peek is closed. |
| Microsoft.PowerToys.Peek_EnablePeek | Occurs when Peek is enabled. |
| Microsoft.PowerToys.Peek_Error | Triggered when an error occurs for Peek. |
| Microsoft.PowerToys.Peek_InvokePeek | Occurs when Peek is invoked. |
| Microsoft.PowerToys.Peek_Opened | Triggered when a Peek window is opened. |
| Microsoft.PowerToys.Peek_OpenWith | Occurs when an item is opened with Peek. |
| Microsoft.PowerToys.Peek_Settings | Triggered when the settings for Peek are modified. |

### PowerRename

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.PowerRename_EnablePowerRename | Triggered when PowerRename is enabled. |
| Microsoft.PowerToys.PowerRename_Invoked | Occurs when PowerRename is invoked. |
| Microsoft.PowerToys.PowerRename_InvokedRet | Triggered when the invocation of PowerRename returns a result. |
| Microsoft.PowerToys.PowerRename_RenameOperation | Triggered during the rename operation within PowerRename. |
| Microsoft.PowerToys.PowerRename_SettingsChanged | Occurs when the settings for PowerRename are changed. |
| Microsoft.PowerToys.PowerRename_UIShownRet | Triggered when the PowerRename user interface is shown. |

### PowerToys Run

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.LauncherBootEvent | Triggered when PowerToys Run is initialized on boot. |
| Microsoft.PowerToys.LauncherColdStateHotkeyEvent | Occurs when the hotkey is pressed in the cold state (not yet initialized). |
| Microsoft.PowerToys.LauncherFirstDeleteEvent | Triggered when the first deletion action is performed in PowerToys Run. |
| Microsoft.PowerToys.LauncherHideEvent | Occurs when PowerToys Run is hidden. |
| Microsoft.PowerToys.LauncherQueryEvent | Triggered when a query is made in PowerToys Run. |
| Microsoft.PowerToys.LauncherResultActionEvent | Occurs when an action is taken on a result in PowerToys Run. |
| Microsoft.PowerToys.LauncherShowEvent | Triggered when PowerToys Run is shown. |
| Microsoft.PowerToys.LauncherWarmStateHotkeyEvent | Occurs when the hotkey is pressed in the warm state (initialized). |
| Microsoft.PowerToys.RunPluginsSettingsEvent | Triggered when the settings for PowerToys Run plugins are accessed or modified. |
| Microsoft.PowerToys.WindowWalker_EnableWindowWalker | Triggered when the Window Walker plugin is enabled. |

### Quick Accent

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.PowerAccent_EnablePowerAccent | Triggered when Quick Accent is enabled. |
| Microsoft.PowerToys.PowerAccentShowAccentMenuEvent | Occurs when the accent menu is displayed. |

### Registry Preview

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.RegistryPreview_Activate | Triggered when Registry Preview is activated. |
| Microsoft.PowerToys.RegistryPreview_EnableRegistryPreview | Occurs when Registry Preview is enabled. |

### Screen Ruler

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.MeasureTool_BoundsToolActivated | Triggered when Screen Ruler's Bounds tool is activated. |
| Microsoft.PowerToys.MeasureTool_EnableMeasureTool | Occurs when Screen Ruler is enabled. |
| Microsoft.PowerToys.MeasureTool_MeasureToolActivated | Triggered when Screen Ruler's Measure tool is activated. |

### Shortcut Guide

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.ShortcutGuide_GuideSession | Logs a Shortcut Guide session including duration and how it was closed. |
| Microsoft.PowerToys.ShortcutGuide_Settings | Indicates a change in the settings related to the Shortcut Guide. |

### Text Extractor

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.PowerOCR_EnablePowerOCR | Triggered when the Text Extractor (OCR) feature is enabled. |
| Microsoft.PowerToys.PowerOCRCancelledEvent | Occurs when the text extraction process is cancelled. |
| Microsoft.PowerToys.PowerOCRCaptureEvent | Occurs when the user has created a capture for text extraction. |
| Microsoft.PowerToys.PowerOCRInvokedEvent | Triggered when Text Extractor is invoked. |

### Workspaces

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.Projects_CLIUsage | Logs usage of command-line arguments for launching apps. |
| Microsoft.PowerToys.Workspaces_CreateEvent | Triggered when a new workspace is created. |
| Microsoft.PowerToys.Workspaces_DeleteEvent | Triggered when a workspace is deleted. |
| Microsoft.PowerToys.Workspaces_EditEvent | Triggered when a workspace is edited or modified. |
| Microsoft.PowerToys.Workspaces_Enable | Indicates that Workspaces is enabled. |
| Microsoft.PowerToys.Workspaces_LaunchEvent | Triggered when a workspace is launched. |
| Microsoft.PowerToys.Workspaces_Settings | Logs changes to workspaces settings. |

### ZoomIt

| Event Name | Description |
| --- | --- |
| Microsoft.PowerToys.ZoomIt_EnableZoomIt | Triggered when ZoomIt is enabled/disabled. |
| Microsoft.PowerToys.ZoomIt_Started | Triggered when the ZoomIt process starts. |
| Microsoft.PowerToys.ZoomIt_ActivateBreak | Triggered when the Break mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateDraw | Triggered when the Draw mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateZoom | Triggered when the Zoom mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateLiveZoom | Triggered when the Live Zoom mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateDemoType | Triggered when the DemoType mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateRecord | Triggered when the Record mode is entered. |
| Microsoft.PowerToys.ZoomIt_ActivateSnip | Triggered when the Snip mode is entered. |

<!-- back up of table

| Event Name | Description |
| --- | --- |
| x | x |
| x | x |
| x | x |

-->
