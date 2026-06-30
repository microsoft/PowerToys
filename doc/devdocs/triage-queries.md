# Triage Queries

## Untriaged

- [Team follow up needed](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+label%3ANeeds-Team-Response)
- [Triage needed](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+is%3Aopen+label%3ATriage-Needed)
- [Backlog issues, not backlogged](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+is%3Aopen+-label%3AIdea-Enhancement+-label%3A%22Idea-New+PowerToy%22+-label%3AResolution-Fix-Committed+-milestone%3ABacklog+)

## Localization issues

- [Loc issues that need followup](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue%20is%3Aopen%20-label%3A%22Loc-Sent%20To%20Team%22%20-label%3A%22Resolution-Fix%20Committed%22%20%20label%3AArea-Localization%20label%3AIssue-Translation)
  - [Direct loc template [msft only]](https://aka.ms/gsh_openabug)

## Watson queries [msft only]

### Catch all

- [Catch all](https://watsonportal.microsoft.com/CabSearch?=&DateTimeFormat=UTC&MaxRows=1000&AppScope_AppVersion=0.100.0.0&Process=*powertoys*)

> [!NOTE]
> Update the `AppScope_AppVersion` (and the installer file names below) to the current release version when triaging.

### EXE name

- [Machine installer (x64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysSetup-0.100.0-x64.exe)
- [Machine installer (arm64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysSetup-0.100.0-arm64.exe)
- [User installer (x64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysUserSetup-0.100.0-x64.exe)
- [User installer (arm64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysUserSetup-0.100.0-arm64.exe)
- [Main exe](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppVersion=0.100.0&AppScope_AppName=PowerToys.exe)
- [PT Run / example](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppVersion=0.100.0&AppScope_AppName=PowerToys.PowerLauncher.exe)

### DLL based

- [KBM](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.0.0&FailureSearchText=keyboardmanager.dll)
- [Power Preview](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.0.0&FailureSearchText=powerpreview.dll)
- [SVG Thumbnail](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.0.0&FailureSearchText=SvgThumbnailProvider.dll)
- [SVG Preview Pane](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.0.0&FailureSearchText=SvgPreviewHandler.dll)
- [Markdown Preview Pane](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.0.0&FailureSearchText=MarkdownPreviewHandler.dll)

## Stock responses

### Issue is a duplicate

> Thank you for your issue! We've linked your report against another issue #
>
> Thanks for helping us to make PowerToys a better piece of software.

---

## Common triage issues

These are items that are asked for a lot and are the main tracking issues.

### General

| Issue | Description | Status |
|---|---|---|
| [#21642](https://github.com/microsoft/PowerToys/issues/21642) | Add shortcuts to individual PowerToys on Start etc. | 🗒️Proposed |

### New PowerToys

| Category | Issue | Description | Status |
|---|---|---|---|
| Text | [#671](https://github.com/microsoft/PowerToys/issues/671) | Enhanced Clipboard | 🗒️Proposed |
| Text | [#907](https://github.com/microsoft/PowerToys/issues/907) | Change text Case | 🗒️Proposed |
| Text | [#1758](https://github.com/microsoft/PowerToys/issues/1758) | Text Snippets | 🗒️Proposed |
| Text | [#5074](https://github.com/microsoft/PowerToys/issues/5074) | Text Replacement / expander | 🗒️Proposed |
| Text | [#30902](https://github.com/microsoft/PowerToys/issues/30902) | Keyboard Remap to send variables | 🗒️Proposed |
| Files | [#2686](https://github.com/microsoft/PowerToys/issues/2686) | Convert images | 🗒️Proposed |
| Files | [#3442](https://github.com/microsoft/PowerToys/issues/3442) | Media converter with ffmpg | 🗒️Proposed |
| Files | [#3098](https://github.com/microsoft/PowerToys/issues/3098) | Paste as Files | 🗒️Proposed |
| Files | [#23018](https://github.com/microsoft/PowerToys/issues/23018) | Image Compression | 🗒️Proposed |
| Network | [#828](https://github.com/microsoft/PowerToys/issues/828) | Network speed in task bar | 🗒️Proposed |
| Windows | [#269](https://github.com/microsoft/PowerToys/issues/269) | Alt+Drag or Windows+Drag to move windows | 🗒️Proposed |
| Windows | [#278](https://github.com/microsoft/PowerToys/issues/278) | Keyboard shortcut for switching between windows of the same program | 🗒️Proposed |
| Windows | [#1084](https://github.com/microsoft/PowerToys/issues/1084) | Make windows Acrylic / Transparent | 🗒️Proposed |
| Windows | [#13035](https://github.com/microsoft/PowerToys/issues/13035) | Fade other windows | 🗒️Proposed |
| Taskbar | [#264](https://github.com/microsoft/PowerToys/issues/264) | Grouping Taskbar Icons | 🗒️Proposed |
| Virtual Desktops | [#16](https://github.com/microsoft/PowerToys/issues/16) | Move Window to specific virtual desktop | 🗒️Proposed |
| Virtual Desktops | [#19](https://github.com/microsoft/PowerToys/issues/19) | Jump to specific virtual desktop | 🗒️Proposed |
| Virtual Desktops | [#24](https://github.com/microsoft/PowerToys/issues/24) | Assign app to specific virtual desktop | 🗒️Proposed |
| Virtual Desktops | [#32](https://github.com/microsoft/PowerToys/issues/32) | Remember last virtual desktop | 🗒️Proposed |
| Virtual Desktops | [#3737](https://github.com/microsoft/PowerToys/issues/3737) | Taskbar widget to view currently selected virtual desktop | 🗒️Proposed |
| Keyboard | [#981](https://github.com/microsoft/PowerToys/issues/981) | Show what keys are pressed | 🗒️Proposed |
| Keyboard | [#20445](https://github.com/microsoft/PowerToys/issues/20445) | Keyboard lock | ❌Won't Fix |
| System | [#119](https://github.com/microsoft/PowerToys/issues/119) | Power Plan Switching (automatic too) | 🗒️Proposed |
| System | [#1331](https://github.com/microsoft/PowerToys/issues/1331) | Light/dark mode switch based on key, time or sunset/sunrise | 🗒️Proposed |
| File Explorer | [#33](https://github.com/microsoft/PowerToys/issues/33) | Manage right-click / customize context menu | 🗒️Proposed |
| File Explorer | [#150](https://github.com/microsoft/PowerToys/issues/150) | Improved file tagging system | 🗒️Proposed |
| File Explorer | [#356](https://github.com/microsoft/PowerToys/issues/356) | Show folder size in explorer | 🗒️Proposed |

### Always On Top

| Issue | Description | Status |
|---|---|---|
| [#15476](https://github.com/microsoft/PowerToys/issues/15476) | Always On Top keeps windows on top even if switched off again | 🗒️Proposed |
| [#28072](https://github.com/microsoft/PowerToys/issues/28072) | Transparent and On top | 🗒️Proposed |
| [#28120](https://github.com/microsoft/PowerToys/issues/28120) | Always on Top, frame still remains | 🗒️Proposed |

### Awake

| Issue | Description | Status |
|---|---|---|
| [#11996](https://github.com/microsoft/PowerToys/issues/11996) | Icon tells the state | 🗒️Proposed |
| [#27790](https://github.com/microsoft/PowerToys/issues/27790) | Working Hours only | 🗒️Proposed |

### Color Picker

| Issue | Description | Status |
|---|---|---|
| [#11585](https://github.com/microsoft/PowerToys/issues/11585) | Incorrect color Profiles | 🗒️Proposed |

### FancyZones

| Issue | Description | Status |
|---|---|---|
| [#4](https://github.com/microsoft/PowerToys/issues/4) | Full window manager including specific layouts for docking and undocking laptops | 🗒️Proposed |
| [#8](https://github.com/microsoft/PowerToys/issues/8) | Window auto-alignment/snap/magnet to other windows and screen edges | 🗒️Proposed |
| [#254](https://github.com/microsoft/PowerToys/issues/254) | Auto resizing of windows placed side-by-side | 🗒️Proposed |
| [#279](https://github.com/microsoft/PowerToys/issues/279) | Maximize/Full Screen window within a zone (virtual monitor) | 🗒️Proposed |
| [#348](https://github.com/microsoft/PowerToys/issues/348) | Choose apps to open up for a specific layout | 🗒️Proposed |
| [#376](https://github.com/microsoft/PowerToys/issues/376) | Make FancyZones touch friendly | 🗒️Proposed |
| [#463](https://github.com/microsoft/PowerToys/issues/463) | Allow manual coordinates (absolute and relative) | 🗒️Proposed |
| [#492](https://github.com/microsoft/PowerToys/issues/492) | Map keyboard shortcuts to zones directly | 🗒️Proposed |
| [#2830](https://github.com/microsoft/PowerToys/issues/2830) | Custom Dragging Key, use ctrl, use right click | 🗒️Proposed |
| [#4235](https://github.com/microsoft/PowerToys/issues/4235) | Add default launch zones per application | 🗒️Proposed |
| [#18814](https://github.com/microsoft/PowerToys/issues/18814) | Move newly created windows to the last known zone | 🗒️Proposed |
| [#24995](https://github.com/microsoft/PowerToys/issues/24995) | Changing custom keyboard shortcuts for FancyZones | 🗒️Proposed |
| [#25220](https://github.com/microsoft/PowerToys/issues/25220) | Not filling the zones | 🗒️Proposed |
| [#25719](https://github.com/microsoft/PowerToys/issues/25719) | Show a thumbnail of the FancyZones during drag and drop | 🗒️Proposed |

### Image Resizer

| Issue | Description | Status |
|---|---|---|
| [#1934](https://github.com/microsoft/PowerToys/issues/1934) | Better support for HEIC image formats | 🗒️Proposed |
| [#26931](https://github.com/microsoft/PowerToys/issues/26931) | Simple crop to width/height option in image resizer | ❌Won't Fix |

### Keyboard Manager

| Issue | Description | Status |
|---|---|---|
| [#1460](https://github.com/microsoft/PowerToys/issues/1460) | Multi-Keyboard Support | ❌Won't Fix |
| [#1556](https://github.com/microsoft/PowerToys/issues/1556) | Use KBM to launch keys w/ arguments | ✅Implemented |
| [#1881](https://github.com/microsoft/PowerToys/issues/1881) | Swappable Profiles | 🗒️Proposed |
| [#3326](https://github.com/microsoft/PowerToys/issues/3326) | Caps Lock as a modifier | 🗒️Proposed |
| [#3350](https://github.com/microsoft/PowerToys/issues/3350) | Launch apps with keyboard shortcuts | ✅Implemented |
| [#3364](https://github.com/microsoft/PowerToys/issues/3364) | Remap Key to Mouse Click | 🗒️Proposed |
| [#3481](https://github.com/microsoft/PowerToys/issues/3481) | Allow users to have a shortcut not end with a modifier key | 🗒️Proposed |
| [#3619](https://github.com/microsoft/PowerToys/issues/3619) | Use Tab or Space as modifier key | 🗒️Proposed |
| [#3826](https://github.com/microsoft/PowerToys/issues/3826) | Support mapping to use certain symbols (Unicode) | 🗒️Proposed |
| [#3936](https://github.com/microsoft/PowerToys/issues/3936) | Adding ability to input Triple modifier shortcuts | 🗒️Proposed |
| [#4452](https://github.com/microsoft/PowerToys/issues/4452) | Exporting and loading Key remap file / presets | 🗒️Proposed |
| [#4508](https://github.com/microsoft/PowerToys/issues/4508) | Chord / Dead key support | ✅Implemented |
| [#4879](https://github.com/microsoft/PowerToys/issues/4879) | Toggle key for Keyboard Manager | 🗒️Proposed |
| [#5074](https://github.com/microsoft/PowerToys/issues/5074) | Using KBM to replace text | 🗒️Proposed |
| [#5670](https://github.com/microsoft/PowerToys/issues/5670) | Remap shortcuts without action keys | 🗒️Proposed |
| [#5722](https://github.com/microsoft/PowerToys/issues/5722) | Allow a remap to engage multiple shortcuts | 🗒️Proposed |
| [#6223](https://github.com/microsoft/PowerToys/issues/6223) | Specify multiple target applications | 🗒️Proposed |
| [#6756](https://github.com/microsoft/PowerToys/issues/6756) | App-specific remapping for single Keys | 🗒️Proposed |
| [#6976](https://github.com/microsoft/PowerToys/issues/6976) | Map to custom keys / diacritic character, not just keycode | 🗒️Proposed |
| [#9919](https://github.com/microsoft/PowerToys/issues/9919) | Enable/Disable individual remaps | 🗒️Proposed |
| [#12349](https://github.com/microsoft/PowerToys/issues/12349) | Auto-select keymap according to keyboard being typed on | 🗒️Proposed |
| [#20445](https://github.com/microsoft/PowerToys/issues/20445) | Keyboard lock | ❌Won't Fix |

### Peek

| Issue | Description | Status |
|---|---|---|
| [#26143](https://github.com/microsoft/PowerToys/issues/26143) | Use just space | 🗒️Proposed |
| [#26146](https://github.com/microsoft/PowerToys/issues/26146) | Preview files within a Zip | 🗒️Proposed |
| [#26210](https://github.com/microsoft/PowerToys/issues/26210) | Preview Office Files | ✅Implemented |
| [#26760](https://github.com/microsoft/PowerToys/issues/26760) | Open unknown files as Text | 🗒️Proposed |

### PowerRename

| Issue | Description | Status |
|---|---|---|
| [#8484](https://github.com/microsoft/PowerToys/issues/8484) | Variable pattern notation for parent folder | 🗒️Proposed |
| [#9755](https://github.com/microsoft/PowerToys/issues/9755) | Convert case / uppercase, lowercase, etc. | 🗒️Proposed |

### PowerToys Run

| Issue | Description | Status |
|---|---|---|
| [#1605](https://github.com/microsoft/PowerToys/issues/1605) | Option to Run as different user | ✅Implemented |
| [#1912](https://github.com/microsoft/PowerToys/issues/1912) | List browser tabs for running processes | ☑3rd party plugin |
| [#2408](https://github.com/microsoft/PowerToys/issues/2408) | Ability to execute system/OS commands like shutdown | ✅Implemented |
| [#2451](https://github.com/microsoft/PowerToys/issues/2451) | Implement exact phrase | ✅Implemented |
| [#3046](https://github.com/microsoft/PowerToys/issues/3046) | Close windows through WindowWalker | ❌Won't Fix |
| [#3082](https://github.com/microsoft/PowerToys/issues/3082) | Search programs based on context | ✅Implemented |
| [#3229](https://github.com/microsoft/PowerToys/issues/3229) | PowerToys Run should remember frequently used apps | ✅Implemented |
| [#3233](https://github.com/microsoft/PowerToys/issues/3233) | Add settings to only enable currently running apps | 🗒️Proposed |
| [#3245](https://github.com/microsoft/PowerToys/issues/3245) | Be able to launch a web search | ✅Implemented |
| [#3269](https://github.com/microsoft/PowerToys/issues/3269) | Allow using a single key as a trigger | 🗒️Proposed |
| [#3309](https://github.com/microsoft/PowerToys/issues/3309) | Add dictionary | 🗒️Proposed |
| [#3311](https://github.com/microsoft/PowerToys/issues/3311) | Add ability to include/exclude certain drives/folders | ❌Won't Fix |
| [#3347](https://github.com/microsoft/PowerToys/issues/3347) | Have special characters to filter/change behavior | ✅Implemented |
| [#3357](https://github.com/microsoft/PowerToys/issues/3357) | Rethink integration of window walker into launcher | ❌Won't Fix |
| [#3386](https://github.com/microsoft/PowerToys/issues/3386) | Add control panel applets | ✅Implemented |
| [#3540](https://github.com/microsoft/PowerToys/issues/3540) | Option to change position of the search box | 🗒️Proposed |
| [#3838](https://github.com/microsoft/PowerToys/issues/3838) | Default hotkey conflicts with window menu hotkey | ❌Won't Fix |
| [#3884](https://github.com/microsoft/PowerToys/issues/3884) | Explicitly start new program instance with modifier key | 🗒️Proposed |
| [#4544](https://github.com/microsoft/PowerToys/issues/4544) | Allow for indexing of network drives | 🗒️Proposed |
| [#4599](https://github.com/microsoft/PowerToys/issues/4599) | Add support for time/time zone conversions | ✅Implemented |
| [#4931](https://github.com/microsoft/PowerToys/issues/4931) | Support audio input | ❌Won't Fix |
| [#4950](https://github.com/microsoft/PowerToys/issues/4950) | Support for Run commands | ✅Implemented |
| [#5104](https://github.com/microsoft/PowerToys/issues/5104) | Exclude unwanted file extensions | 🗒️Proposed |
| [#5273](https://github.com/microsoft/PowerToys/issues/5273) | Plugin management section (enable/disable) | ✅Implemented |
| [#5531](https://github.com/microsoft/PowerToys/issues/5531) | Add possibility to filter running processes | ✅Implemented |
| [#5712](https://github.com/microsoft/PowerToys/issues/5712) | Search for folders only | 🗒️Proposed |

### Shortcut Guide

| Issue | Description | Status |
|---|---|---|
| [#129](https://github.com/microsoft/PowerToys/issues/129) | Show All Application Shortcuts | 🗒️Proposed |
| [#15405](https://github.com/microsoft/PowerToys/issues/15405) | PowerToys Shortcuts in Shortcut Guide | 🗒️Proposed |

### Text Extractor

| Issue | Description | Status |
|---|---|---|
| [#28109](https://github.com/microsoft/PowerToys/issues/28109) | Text extractor translate | 🗒️Proposed |
| [#29643](https://github.com/microsoft/PowerToys/issues/29643) | Equation support / LaTeX support | ❌Won't Fix |

### Quick Accent

| Issue | Description | Status |
|---|---|---|
| [#20312](https://github.com/microsoft/PowerToys/issues/20312) | Add own accented letters and edit the existing ones | 🗒️Proposed |
| [#20393](https://github.com/microsoft/PowerToys/issues/20393) | Customizing and sorting | 🗒️Proposed |
| [#20618](https://github.com/microsoft/PowerToys/issues/20618) | ¿ and ¡ with ? and ! keys instead of , | 🗒️Proposed |
| [#20958](https://github.com/microsoft/PowerToys/issues/20958) | Change activation key | 🗒️Proposed |
| [#22205](https://github.com/microsoft/PowerToys/issues/22205) | Global enable/disable | 🗒️Proposed |
| [#28723](https://github.com/microsoft/PowerToys/issues/28723) | Hold Key, better experience | 🗒️Proposed |
