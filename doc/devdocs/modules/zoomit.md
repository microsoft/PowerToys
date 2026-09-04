# ZoomIt Module

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/zoomit)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-ZoomIt)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-ZoomIt%20label%3AIssue-Bug%20)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-ZoomIt)<br>
[UI test coverage and execution](../../../src/modules/ZoomIt/Tests/ZoomIt.UITests/README.md)

## Overview

ZoomIt is a screen zoom and annotation tool originally from Microsoft's Sysinternals suite. It allows users to:

- Zoom in on specific areas of the screen
- Draw and annotate on the screen while zoomed in
- Use a timer for presentations or breaks
- Pan and move while zoomed in
- Record screen activity with audio

ZoomIt runs in the background and is activated via customizable hotkeys.

## Special Integration Considerations

Unlike other PowerToys modules, ZoomIt has some unique integration aspects:

1. **Registry-based Settings**: ZoomIt uses registry settings instead of JSON files (which is the standard for other PowerToys modules). This was required to maintain compatibility with the standalone Sysinternals version.

2. **Dual Source of Truth**: The PowerToys repository serves as the source of truth for both the PowerToys version and the standalone Sysinternals version, with build flags to differentiate between them.

3. **Settings Integration**: A special WinRT/C++ interop library was developed to bridge between PowerToys' JSON-based settings system and ZoomIt's registry-based settings.

## Technical Architecture

The ZoomIt module consists of the following components:

1. **ZoomIt Executable** (`PowerToys.ZoomIt.exe`): The main ZoomIt application that provides the zooming and annotation functionality.

2. **Module Interface** (`PowerToys.ZoomItModuleInterface.dll`): Implements the PowerToys module interface to integrate with the PowerToys runner.

3. **Settings Interop** (`ZoomItSettingsInterop`): A WinRT/C++ interop library that enables communication between PowerToys settings and ZoomIt's registry settings.

![key functions](../images/zoomit/functions.png)

### Directory Structure

```
src/
├── modules/
│   └── ZoomIt/
│       ├── Tests/ZoomIt.UITests/   # winappcli UI tests and deterministic fixtures
│       ├── ZoomIt/             # Main ZoomIt application code
│       ├── ZoomItModuleInterface/  # PowerToys module interface implementation
│       └── ZoomItSettingsInterop/  # WinRT/C++ interop for settings
├── settings-ui/
│   └── Settings.UI/
│       ├── SettingsXAML/
│       │   └── Views/
│       │       └── ZoomItPage.xaml  # ZoomIt settings page UI
│       └── ViewModels/
│           └── ZoomItViewModel.cs   # ZoomIt settings view model
└── common/
    └── sysinternals/          # Common code from Sysinternals
```


## Settings Management

ZoomIt's settings are stored in the Windows registry instead of JSON files to maintain compatibility with the standalone version. The settings include:

- Hotkey combinations for different modes (zoom, draw, etc.)
- Drawing options (colors, line thickness, etc.)
- Font settings for text annotations
- Microphone selection for recording
- Custom file paths for demo mode and break backgrounds

The `ZoomItSettingsInterop` library handles:
1. Loading settings from registry and converting to JSON for PowerToys settings UI
2. Saving changes from the settings UI back to the registry
3. Notifying the ZoomIt application when settings change

![interop](../images/zoomit/interop.png)

## Integration Steps

The integration of ZoomIt into PowerToys involved these key steps:

1. **Code Migration**:
   - Moving code from the Sysinternals ZoomIt repository to `src/modules/ZoomIt/ZoomIt`
   - Adding required common libraries to `src/common/sysinternals`
   - Sanitizing code for open source (removing private APIs, undocumented details, etc.)
   - Ensuring no private APIs (validated through APIScan)
   - Removing references to undocumented implementation details, constants, and names
   - Standardizing dependencies with other PowerToys utilities

2. **Module Interface Implementation**:
   - Creating the PowerToys module interface
   - Adding process management (start/terminate)
   - Implementing event-based communication for settings updates
   - Adding named events for communication between PowerToys and ZoomIt

3. **Settings Integration**:
   - Extracting ZoomIt settings code to a shareable component
   - Creating a WinRT/C++ interop library for registry-JSON conversion
   - Implementing all settings UI controls in PowerToys settings
   - Building `ZoomItSettingsInterop` as a bridge between registry and JSON settings

4. **PowerToys Integration**:
   - Adding ZoomIt to the PowerToys runner
   - Adding GPO rules for ZoomIt
   - Implementing telemetry and logging
   - Creating OOBE (out-of-box experience) page with animated tutorial
   - Adding ZoomIt to process termination list for proper cleanup
   - Adding telemetry events documentation

5. **UI/UX Adjustments**:
   - Redirecting ZoomIt's settings UI to PowerToys settings
   - Handling hotkey conflicts with warning notifications
   - Modifying tray icon behavior
   - Removing original ZoomIt options menu entries
   - Adding Sysinternals attribution on the settings page

6. **Build System Updates**:
   - Adding ZoomIt to the PowerToys solution
   - Implementing build flags for standalone vs. PowerToys versions
   - Adding signing for new binaries
   - Fixing analyzer errors and code quality issues

## Debug Instructions
1. Build the entire PowerToys solution at least once.
2. Set `runner` as the startup project and start debugging.
3. Once the PowerToys Settings app is running and ensure ZoomIt is activated.
4. Set `ZoomIt` as the startup project in Visual Studio.
5. Press `Ctrl + Alt + P` and attach ZoomIt to the process.
6. You should now be able to set breakpoints and step through the code.

## Special Implementation Details

### Snip With Dictation

Enable dictation in ZoomIt's SNIP options, then `Ctrl+6` and start dragging. Tapping `Space`
at any point during the drag opens the microphone, and on mouse up the captured image and
the transcription are published together. A single paste then delivers a screenshot plus a
spoken annotation or instruction into targets such as Copilot Chat.

#### Delivering both halves in one paste

`ClipboardPayload` publishes the snip in every format that can carry it. Only two mainstream
Windows clipboard formats carry text and an image in a single payload, and both are
published: `CF_HTML` (with the image inlined as a base64 `<img>`) and `Rich Text Format`
(with the image embedded as a `\pict\pngblip`). Everything else on the clipboard is
single-payload by definition, so there is no third option. `CF_HTML` and RTF are published
first, ahead of `CF_DIB` and `CF_UNICODETEXT`, because clipboard enumeration order is
`SetClipboardData` call order and some consumers take the first format they recognize.

That is enough for applications that read a rich format - OneNote and Word paste the image
and the text together. It is not enough for chat surfaces. The GitHub Copilot CLI and the
Visual Studio Code chat input choose exactly one payload, and prefer text: the CLI's paste
handler reads the clipboard text and, if it is non-empty, inserts it and never looks at the
image; only when the text is empty does it fall through to attaching the image. No
combination of clipboard formats changes that, because the choice is made in the
application, not by the clipboard.

`PasteChaperone` closes that gap by splitting the user's single gesture into two pastes. It
publishes the image on its own, installs a low level keyboard hook, and waits. When it sees
a physical `Ctrl+V` it stops hooking, lets the target consume the image, then swaps the
clipboard to the transcription and synthesizes a second `Ctrl+V`. Once that is delivered the
full multi-format payload is restored so a later paste into OneNote or Word still gets
everything. The user presses `Ctrl+V` once and receives the screenshot as an attachment
followed by the dictated instruction.

The details that matter:

- The hook must ignore `LLKHF_INJECTED`, or the synthesized paste retriggers the sequence.
- A low level keyboard hook is dispatched on the thread that installed it and that thread
  must be pumping messages, hence the dedicated chaperone thread.
- `SetTimer` with a `NULL` window ignores the requested id and returns a new one, so the
  returned values have to be stored and compared against `msg.wParam`.
- Every stage records `GetClipboardSequenceNumber`. If it no longer matches, the user copied
  something else and the sequence abandons quietly rather than overwriting their clipboard.
- The second paste waits for the physical `Ctrl` to come up first, so the synthesized key
  events do not interleave with the user's own.
- If no paste arrives within a minute the combined payload is restored, leaving the user
  exactly where they would have been had the chaperone never armed.

The behavior is always enabled for dictation snips.

Arming dictation from inside the drag rather than from a global chord is deliberate. The
snip hotkeys have already consumed `Ctrl+6` (snip), `Ctrl+Shift+6` (snip to file) and
`Ctrl+Alt+6` (snip with OCR), leaving only cumbersome combinations. The modal drag loop in
`SelectRectangle` consumed nothing but `ESC`, so a keystroke there costs no global hotkey
namespace, and the user's hand is already on the mouse. It also matches the way the feature
is actually used: the decision to annotate usually comes after the selection is up.

Starting is one way for the duration of a drag; there is no stop, so a stray second tap
cannot discard words that have already been transcribed. Key auto repeat is filtered with
bit 30 of `lParam`.

#### Recognition engines

`DictationSession` tries two engines and silently falls back, which is what lets the
feature work with no setup at all:

1. `Windows.Media.SpeechRecognition`, the inbox projection. More accurate, but available
   only when the process has MSIX package identity. Microsoft documents that unpackaged
   apps cannot use this API; in practice `StartAsync` can succeed and open the microphone
   while producing no hypotheses or results. When identity is present it also requires
   "Online speech recognition" in Privacy settings.
2. SAPI (`CLSID_SpInprocRecognizer`) with a dictation grammar. Entirely on device, needs no
   privacy consent, no package identity and no extra dependencies.

#### Recognition accuracy

The two engines are not close in quality, and the gap is a generation of technology rather
than tuning. A typical Windows 11 machine has exactly one of each installed:

| Category | Token | Engine |
|----------|-------|--------|
| `SPCAT_RECOGNIZERS` (`...\Speech\Recognizers`) | `MS-1033-80-DESK` | Speech Recognizer 8.0, the SAPI fallback |
| `...\Speech_OneCore\Recognizers` | `MS-1033-110-WINMO-DNN` | Embedded DNN v11.1, used by `Windows.Media.SpeechRecognition` |

The DNN engine cannot be reached through SAPI. `ISpRecognizer::SetRecognizer` accepts the
OneCore token and `CreateRecoContext` and `CreateGrammar` both succeed, but
`LoadDictation` then fails with `SPERR_NOT_FOUND` (0x8004503a) because that engine ships no
SAPI dictation topic. It is reachable only through the OneCore speech APIs, which means the
package identity and privacy consent gates are the only way in. Standalone ZoomIt therefore
uses SAPI rather than starting an apparently successful recognizer that returns no text.

Audio capture for recording is independent from dictation. The microphone chooser applies
to both features, but disabling "Include microphone in recording" does not disable
dictation. SAPI resamples whatever the endpoint provides and picks up the user's trained
recognition profile automatically.

`DoPrepare` checks package identity before trying the inbox API. This check is essential:
`CompileConstraintsAsync` and `StartAsync` can both report success in an unpackaged process
even though the API never raises a recognition event. In that case ZoomIt goes directly to
SAPI, avoiding a microphone indicator that misleadingly suggests transcription is working.

When package identity is available, `IsSpeechPrivacyAccepted` reads
`HKCU\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted`. A
missing key means the user was never asked, which the engine treats exactly like a refusal.

The chosen engine is otherwise sticky for the life of the process, because `DoPrepare`
returns early once `m_backend` is set. `DoPrepare` therefore compares the current consent
state against `m_preparedWithConsent`, and the current microphone verdict against
`m_preparedWithDefaultMic`, and re-runs the engine selection when either has changed in a
packaged process.
`Prewarm` is called at the start of every snip, so the upgrade happens on the next snip
rather than the next launch.

#### Microphone selection beats engine quality

`SpeechRecognizer` exposes no way to choose a capture device - it offers only
`CurrentLanguage`, `Constraints`, `Timeouts`, `State`, `UIOptions` and
`ContinuousRecognitionSession` - so it always records from the Windows default. SAPI, by
contrast, can be pointed at a specific endpoint through `SPCAT_AUDIOIN`, which is how
`ResolveAudioInputToken` honours the microphone chosen on the Record page.

When those two disagree, the accurate engine records the wrong device and transcribes
silence. That is strictly worse than the older engine's lower accuracy, so
`SelectedMicrophoneIsSystemDefault` gates the modern engine on the selection matching the
default, and `DoPrepare` reports `Failure::MicrophoneNotDefault` when it does not.

The check compares against the normal default input (`eConsole`).
`SpeechRecognizer` does not use the separate `eCommunications` default, so a headset being
the communications device does not prevent a webcam that is the normal default from using
the enhanced engine. An empty selection, or a default that cannot be read, resolves to
"matches" so the user is never stranded on the weaker engine over a question ZoomIt could
not answer.

Two things are easy to get wrong in the SAPI path and produce an engine that runs happily
while hearing nothing:

- The in process recognizer has no audio source until one is set, and passing `nullptr` to
  `SetInput` does **not** select a default. A capture device has to be resolved explicitly
  from the `SPCAT_AUDIOIN` token category.
- The thread that calls `ISpRecoContext::GetEvents` must have joined the same multithreaded
  apartment the context was created in, so the event loop calls `CoInitializeEx` itself.

#### Choosing the microphone

Dictation captures from the microphone selected on the Record page, so there is a single
microphone setting for the whole application. SAPI identifies capture devices by its own
token ids rather than by the Windows `DeviceInformation` id that ZoomIt persists in
`MicrophoneDeviceId`, and there is no documented mapping between the two, so
`ResolveAudioInputToken` resolves the stored id to a friendly name and matches that against
the `SPCAT_AUDIOIN` token descriptions. Anything that fails to resolve - the empty "default
device" setting, a stale id, or a microphone that has been unplugged - falls back to SAPI's
default token so dictation still works rather than failing outright.

Descriptions are read directly from the token rather than with `SpGetDescription`, because
`sphelper.h` pulls in ATL and a number of deprecated APIs.

The inbox `Windows.Media.SpeechRecognition` engine is the exception: it exposes no API for
selecting a capture device and always uses the system default, so the Record page selection
does not apply when that engine is the active one.

Components:

| File | Responsibility |
|------|----------------|
| `SelectRectangle.{h,cpp}` | Raises `OnDragStarted` / `OnDictateRequested` / `OnSelectionChanged` / `OnDragCompleted` / `OnCancelled` so the caller can react to the drag without owning the modal loop |
| `DictationSession.{h,cpp}` | Speech to text on a dedicated MTA worker thread. The UI thread is blocked inside the modal selection loop, so recognition cannot run on it |
| `DictationBadge.{h,cpp}` | Layered, capture excluded overlay showing listening state and the live transcription, centered on the top edge of the selection and following it as it is dragged |
| `ClipboardPayload.{h,cpp}` | Publishes `CF_HTML`, `Rich Text Format`, `PNG`, `CF_DIB`, `CF_BITMAP`, `CF_HDROP` and `CF_UNICODETEXT` in one clipboard transaction |
| `PasteChaperone.{h,cpp}` | Splits one Ctrl+V into an image paste followed by a text paste, for applications that accept only one of the two |

Notes:

- Format order matters. `CF_HTML` and `Rich Text Format` are published first because they are
  the only formats that carry text and image together, and enumeration order is
  `SetClipboardData` call order; applications otherwise choose whichever format they prefer.
- `Start` only queues work on the recognition thread, so it cannot report whether the
  microphone actually opened. The badge is driven by `OnStatusChanged` instead, and `Stop`
  waits for a queued start to land before deciding there is nothing to finalize; otherwise
  a quick drag silently discards the dictation.
- Dictation is off by default because it opens the microphone. When `SnipDictateEnabled` is
  clear, `Space` during a drag does nothing and a snip behaves exactly as it always has.
- The options UI is deliberately just the enable toggle and the one gesture paste toggle.
  `SnipDictateToggleKey` (a snip that starts already listening), `SnipDictateOnSnip`,
  `SnipDictatePrefix`, `SnipDictateGrace` and `SnipDictateClipboardFormats` remain registry
  only settings.
- The default backend is the inbox `Windows.Media.SpeechRecognition` projection. It needs no
  package identity, so it works in both the PowerToys build and the standalone Sysinternals
  build. A `Microsoft.Windows.AI.Speech` backend is present but compiled out behind
  `ZOOMIT_WINAI_SPEECH`; it requires MSIX package identity and therefore cannot be used by
  the standalone build.
- With nothing transcribed the clipboard receives image formats only, so a silent dictation
  snip behaves like a plain snip.

### Font Selection

ZoomIt requires storing font information as a binary LOGFONT structure in the registry. This required special handling:

- Creating P/Invoke declarations for Windows font APIs
- Base64 encoding the binary data for transfer through JSON
- Using native Windows dialogs for font selection

### Hotkey Management

ZoomIt registers hotkeys through the Windows RegisterHotKey API. Special handling was needed to:

- Detect and notify about hotkey conflicts
- Update hotkeys when settings change
- Support modifier keys

### Process Communication

Communication between PowerToys and ZoomIt uses:
- Command-line arguments to pass PowerToys process ID
- Named events for signaling settings changes and exit requests
- Windows messages for internal ZoomIt state management

## Implementation Challenges

Several challenges were encountered during ZoomIt integration:

1. **First-Run Behavior**:
   - Font loading crashed when no ZoomIt data existed in registry
   - Hotkeys weren't registered on first run with no existing data
   - Implemented safeguards to handle missing registry data

2. **Settings Synchronization**:
   - Modifier keys for shortcuts weren't correctly updated when settings changed
   - Implemented proper event notification for settings changes
   - Added hotkey conflict detection and warnings

3. **File Interaction**:
   - ZoomIt file pickers changed the working directory of the Settings project
   - Fixed to maintain proper directory context

4. **Drawing Issues**:
   - Color settings lacking opacity caused drawing functionality to fail
   - Removed internal state settings that weren't truly editable

5. **Dual-Build Support**:
   - Added build flags to support both PowerToys and standalone Sysinternals versions
   - Implemented different executable properties based on build target

## Source Code Management

The PowerToys repository serves as the source of truth for both PowerToys and Sysinternals standalone versions of ZoomIt. Key repositories involved:

- Utility repo: `https://dev.azure.com/sysinternals/Tools/_git/ZoomIt`
- Common library repo: `https://dev.azure.com/sysinternals/Tools/_git/Common`

The integration process can be tracked through [PR #35880](https://github.com/microsoft/PowerToys/pull/35880) which contains the complete history of changes required to properly integrate ZoomIt.
